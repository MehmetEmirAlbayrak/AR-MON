"""Flask backend server for AR-MON biome detection with monocular depth estimation."""

import io
import itertools
import json
import logging
import os
import queue
import threading
import time
from pathlib import Path
from typing import Any, Optional

import numpy as np
import torch
import torch.nn.functional as F
from flask import Flask, jsonify, request, send_file
from flask_cors import CORS
from PIL import Image
from torchvision.ops import nms as tv_nms
from ultralytics import YOLO

from torchvision import models, transforms

from metrics import MetricsCollector

# ======================
#  LOGGING SETUP
# ======================

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)

# ======================
#  CONSTANTS
# ======================

# Raw detection pass — low conf so we collect candidates for class-aware NMS.
# Must not exceed the lowest calibrated per-class threshold (tree = 0.20),
# otherwise those detections are dropped before the per-class filter sees them.
RAW_CONF_THRESHOLD: float = 0.20
# Built-in YOLO NMS disabled (iou=0.99) so we apply our own per-class NMS
RAW_IOU_THRESHOLD: float = 0.99

YOLO_IMGSZ: int = int(os.getenv("YOLO_IMGSZ", "640"))

# Per-class IoU thresholds for NMS post-processing
CLASS_IOU_THRESHOLDS: dict[str, float] = {
    "tree":  0.45,   # Discrete objects — standard suppression
    "rock":  0.45,
    "bush":  0.60,   # Can legitimately overlap — more permissive
    "leaf":  0.60,
    "grass": 0.65,   # Covers large areas — most permissive
}

# Biome scoring weight matrix: class -> {biome -> weight}
BIOME_WEIGHTS: dict[str, dict[str, float]] = {
    "tree":  {"forest": 1.5,  "grassland": -0.5, "rocky": -0.5, "mixed":  0.2},
    "leaf":  {"forest": 1.2,  "grassland":  0.0, "rocky": -0.3, "mixed":  0.1},
    "bush":  {"forest": 0.8,  "grassland":  0.5, "rocky": -0.2, "mixed":  0.2},
    "grass": {"forest": 0.2,  "grassland":  1.5, "rocky": -0.3, "mixed":  0.1},
    "rock":  {"forest": -0.3, "grassland": -0.3, "rocky":  1.5, "mixed":  0.3},
}

VEGETATION_CLASSES: frozenset[str] = frozenset({"tree", "leaf", "bush", "grass"})
ROCKY_CLASSES: frozenset[str] = frozenset({"rock"})
FOREST_DIVERSITY_CLASSES: frozenset[str] = frozenset({"tree", "leaf", "bush"})

DIVERSITY_BONUS_FOREST: float = 0.4
DIVERSITY_BONUS_ROCKY: float = 0.3
MIXED_SCORE_MULTIPLIER: float = 0.8
BIOME_UNKNOWN_THRESHOLD: float = 0.5

# Temporal smoothing — confidence-weighted, sticky (hysteresis)
TEMPORAL_SMOOTHING_ENABLED: bool = os.getenv("TEMPORAL_SMOOTHING", "1") == "1"
SMOOTHING_DECAY: float = float(os.getenv("SMOOTHING_DECAY", "0.85"))   # per-frame decay; higher = stickier (holds biome through brief detection gaps)
SWITCH_MARGIN: float = float(os.getenv("SWITCH_MARGIN", "1.3"))        # challenger must beat held biome by this factor to switch
RELEASE_FLOOR: float = float(os.getenv("RELEASE_FLOOR", "0.15"))       # release to 'unknown' when held evidence falls below this

# Image validation
MIN_IMAGE_DIM: int = 64
MAX_IMAGE_DIM: int = 4096

# Depth estimation
DEPTH_ENABLED: bool = os.getenv("DEPTH_ENABLED", "true").lower() == "true"
DEPTH_STRATEGY: str = "median_foreground"

# Debug: save received frames to debug_frames/ for inspection (off by default; set DEBUG_SAVE_FRAMES=1).
DEBUG_SAVE_FRAMES: bool = os.getenv("DEBUG_SAVE_FRAMES", "").lower() in ("1", "true", "yes")
DEBUG_FRAMES_DIR: Path = Path("debug_frames")
DEBUG_FRAMES_KEEP: int = int(os.getenv("DEBUG_FRAMES_KEEP", "30"))

# Calibrated thresholds path and fallback defaults.
# Fallbacks mirror calibrated_thresholds.json (v6 calibration on dataset_v3/val,
# F0.5 grid search; rock manually lowered to 0.35 for real-world recall) so
# behavior is identical even if the JSON file is missing.
CALIBRATED_THRESHOLDS_PATH: Path = Path(os.getenv("CALIBRATED_THRESHOLDS", "calibrated_thresholds.json"))
DEFAULT_CLASS_THRESHOLDS: dict[str, float] = {
    "tree": 0.20,
    "leaf": 0.30,
    "grass": 0.40,
    "bush": 0.60,
    "rock": 0.35,
}

# ======================
#  THRESHOLD LOADING
# ======================


def load_class_thresholds() -> dict[str, float]:
    """
    Load calibrated per-class confidence thresholds, with hardcoded fallback.

    Reads from calibrated_thresholds.json if present (produced by calibrate_thresholds.py).
    Falls back to DEFAULT_CLASS_THRESHOLDS when the file is absent.

    Args:
        None

    Returns:
        Dict mapping class name (str) to confidence threshold (float).
    """
    if CALIBRATED_THRESHOLDS_PATH.exists():
        with open(CALIBRATED_THRESHOLDS_PATH) as f:
            data = json.load(f)
        thresholds = {cls: v["threshold"] for cls, v in data["thresholds"].items()}
        logger.info(f"Loaded calibrated thresholds from {CALIBRATED_THRESHOLDS_PATH}")
        return thresholds
    logger.warning("calibrated_thresholds.json not found — using hardcoded defaults")
    return DEFAULT_CLASS_THRESHOLDS.copy()


CLASS_CONFIDENCE_THRESHOLDS: dict[str, float] = load_class_thresholds()

# ======================
#  APP + CORS
# ======================

app = Flask(__name__)
CORS(app)

# ======================
#  METRICS COLLECTOR
# ======================
# Rolling aggregator for measured latency/depth evidence, exposed via GET /metrics.
metrics = MetricsCollector(maxlen=int(os.getenv("METRICS_WINDOW", "2000")))

# ======================
#  YOLO MODEL LOADING
# ======================

logger.info("Loading YOLO model...")
YOLO_WEIGHTS = os.getenv(
    "YOLO_WEIGHTS",
    os.path.join("models", "best.pt"),
)
yolo_model = YOLO(YOLO_WEIGHTS)

# ======================
#  RESNET BIOME CLASSIFIER
# ======================
RESNET_CHECKPOINT = os.getenv(
    "RESNET_WEIGHTS",
    "models/biome_resnet18.pth"
)
RESNET_CLASSES_JSON = os.getenv("RESNET_CLASSES", "models/class_indices.json")

resnet_model: Optional[torch.nn.Module] = None
resnet_classes: list[str] = []
resnet_transform = transforms.Compose([
    transforms.Resize((256, 256)),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
    # IMPORTANT: training (train_yolo_improved.py / train_biome_classifier.py) used ToTensor()
    # ONLY — no ImageNet Normalize. Inference MUST match training or the ResNet receives
    # out-of-distribution inputs and biome predictions become unstable/wrong. Re-add Normalize
    # here only if the checkpoint is retrained with the identical Normalize step.
])

if os.path.exists(RESNET_CHECKPOINT) and os.path.exists(RESNET_CLASSES_JSON):
    try:
        logger.info("Loading ResNet-18 biome classifier...")
        with open(RESNET_CLASSES_JSON) as f:
            idx_to_class = json.load(f)
        resnet_classes = [idx_to_class[str(i)] for i in range(len(idx_to_class))]

        _resnet = models.resnet18(weights=None)
        _resnet.fc = torch.nn.Linear(_resnet.fc.in_features, len(resnet_classes))
        _resnet.load_state_dict(torch.load(RESNET_CHECKPOINT, map_location="cpu", weights_only=True))
        _resnet.eval()
        resnet_model = _resnet
        logger.info(f"ResNet-18 loaded. Classes: {resnet_classes}")
    except Exception as exc:
        logger.warning(f"Could not load ResNet-18: {exc}. Using rule-based biome only.")
else:
    logger.warning("ResNet checkpoint not found — using rule-based biome classification only.")

# Warm-up: single dummy inference to avoid cold-start latency on first real request
_warmup_dummy = np.zeros((640, 640, 3), dtype=np.uint8)
yolo_model(_warmup_dummy, verbose=False)
logger.info("Model warmed up.")

# ======================
#  DEPTH MODEL (MiDaS)
# ======================

depth_model: Optional[torch.nn.Module] = None
depth_transform = None

if DEPTH_ENABLED:
    logger.info("Loading MiDaS depth model...")
    try:
        depth_model = torch.hub.load(
            "intel-isl/MiDaS",
            "MiDaS_small",   # ~20 MB, fastest variant
            trust_repo=True,
        )
        depth_model.eval()
        depth_model = depth_model.to(torch.device("cpu"))  # GPU reserved for YOLO

        _midas_transforms = torch.hub.load("intel-isl/MiDaS", "transforms", trust_repo=True)
        depth_transform = _midas_transforms.small_transform

        logger.info("MiDaS depth model loaded successfully.")
    except Exception as exc:
        logger.warning(
            f"Could not load MiDaS: {exc}. "
            "Depth will be estimated from bounding box geometry (heuristic fallback)."
        )
        depth_model = None
        depth_transform = None
else:
    logger.info("Depth estimation disabled (DEPTH_ENABLED=false). Using heuristic fallback.")

# ======================
#  DEPTH FUNCTIONS
# ======================


def estimate_depth_map(pil_image: Image.Image) -> Optional[np.ndarray]:
    """
    Run MiDaS monocular depth estimation on a PIL image.

    Higher values in the returned array indicate objects closer to the camera.
    Returns None when the depth model is not loaded.

    Args:
        pil_image: Input RGB PIL image of any size.

    Returns:
        2D numpy array (H, W) with relative depth values normalized to [0, 1],
        where 1.0 = closest to camera. Returns None if model unavailable.
    """
    if depth_model is None or depth_transform is None:
        return None

    img_rgb = np.array(pil_image)
    with torch.no_grad():
        input_batch = depth_transform(img_rgb)
        if isinstance(input_batch, dict):
            input_batch = input_batch["image"]
        # Current MiDaS hub transforms already return a batched [1, 3, H, W]
        # tensor; unsqueezing unconditionally produced 5D input and crashed
        # the depth worker (silent fallback to bbox heuristic every frame).
        if input_batch.dim() == 3:
            input_batch = input_batch.unsqueeze(0)

        prediction = depth_model(input_batch)
        prediction = F.interpolate(
            prediction.unsqueeze(1),
            size=pil_image.size[::-1],  # size=(H, W); pil.size is (W, H)
            mode="bicubic",
            align_corners=False,
        ).squeeze()

    depth_map = prediction.cpu().numpy()
    d_min, d_max = depth_map.min(), depth_map.max()
    if d_max > d_min:
        depth_map = (depth_map - d_min) / (d_max - d_min)

    return depth_map


def get_object_depth(
    depth_map: np.ndarray,
    bbox_normalized: list[float],
    image_size: tuple[int, int],
    strategy: str = DEPTH_STRATEGY,
) -> float:
    """
    Extract a single representative depth value for a detected object from the depth map.

    Args:
        depth_map: H×W normalized depth array (1.0 = closest to camera).
        bbox_normalized: [x1, y1, x2, y2] in normalized [0, 1] coordinates.
        image_size: (width, height) of the original image in pixels.
        strategy: Aggregation strategy within the bbox region:
            'mean'              — average of all pixels in bbox
            'median'            — median of all pixels
            'median_foreground' — median of the closest 40% of pixels (best for
                                  objects against sky or background)
            'center_patch'      — median of the central 50% of bbox dimensions

    Returns:
        Depth value in [0.0, 1.0] where 1.0 = very close to camera.
    """
    img_w, img_h = image_size
    x1 = max(0, int(bbox_normalized[0] * img_w))
    y1 = max(0, int(bbox_normalized[1] * img_h))
    x2 = min(img_w, int(bbox_normalized[2] * img_w))
    y2 = min(img_h, int(bbox_normalized[3] * img_h))

    region = depth_map[y1:y2, x1:x2]
    if region.size == 0:
        return 0.5

    if strategy == "mean":
        return float(np.mean(region))
    if strategy == "median":
        return float(np.median(region))
    if strategy == "median_foreground":
        # Keep only the closest 40% of pixels (above the 60th percentile)
        threshold = np.percentile(region, 60)
        foreground = region[region >= threshold]
        return float(np.median(foreground)) if foreground.size > 0 else float(np.median(region))
    if strategy == "center_patch":
        cx1 = x1 + (x2 - x1) // 4
        cy1 = y1 + (y2 - y1) // 4
        cx2 = x2 - (x2 - x1) // 4
        cy2 = y2 - (y2 - y1) // 4
        center = depth_map[cy1:cy2, cx1:cx2]
        return float(np.median(center)) if center.size > 0 else float(np.median(region))
    return float(np.median(region))


def estimate_depth_heuristic(
    bbox_normalized: list[float],
    label: str,
    image_height: int,
) -> float:
    """
    Estimate relative depth from bounding box geometry when the depth model is unavailable.

    Combines three signals:
    1. Bounding box area (larger = closer)
    2. Vertical center position (lower in frame = closer, perspective geometry)
    3. Class-specific size prior (box area relative to expected area at medium distance)

    Args:
        bbox_normalized: [x1, y1, x2, y2] in normalized [0, 1] coordinates.
        label: Object class name (used for size prior lookup).
        image_height: Image height in pixels (reserved for future metric depth scaling).

    Returns:
        Estimated depth in [0.0, 1.0] where 1.0 = very close to camera.
    """
    x1, y1, x2, y2 = bbox_normalized
    bbox_area = (x2 - x1) * (y2 - y1)
    bbox_center_y = (y1 + y2) / 2.0

    # Expected normalized bbox area for an object at "medium" distance
    EXPECTED_AREA: dict[str, float] = {
        "tree":  0.15,
        "bush":  0.08,
        "grass": 0.20,
        "leaf":  0.05,
        "rock":  0.06,
    }
    expected = EXPECTED_AREA.get(label, 0.08)

    area_score = min(bbox_area * 4.0, 1.0)
    vertical_score = bbox_center_y
    size_score = min(bbox_area / expected, 1.0) if expected > 0 else 0.5

    depth = 0.4 * area_score + 0.3 * vertical_score + 0.3 * size_score
    return float(np.clip(depth, 0.0, 1.0))


# ======================
#  ASYNC DEPTH ESTIMATOR
# ======================


class DepthEstimator:
    """
    Asynchronous MiDaS depth estimator running in a background daemon thread.

    Main thread submits frames via submit(); the worker processes the latest
    pending frame and stores the result. Latest result is retrieved with
    get_latest_depth(). If no depth is available yet, returns None.
    """

    def __init__(self, model: torch.nn.Module, transform: Any) -> None:
        """
        Initialize the estimator and start the background worker thread.

        Args:
            model: Loaded MiDaS depth model.
            transform: MiDaS preprocessing transform callable.
        """
        self._model = model
        self._transform = transform
        self._latest_depth: Optional[np.ndarray] = None
        self._last_compute_ms: Optional[float] = None
        self._lock = threading.Lock()
        self._queue: queue.Queue[Image.Image] = queue.Queue(maxsize=2)
        self._thread = threading.Thread(target=self._worker, daemon=True)
        self._thread.start()

    def submit(self, pil_image: Image.Image) -> None:
        """
        Submit an image for background depth estimation, dropping stale pending frames.

        Args:
            pil_image: RGB PIL image to estimate depth for.

        Returns:
            None
        """
        try:
            self._queue.put_nowait(pil_image)
        except queue.Full:
            try:
                self._queue.get_nowait()
            except queue.Empty:
                pass
            self._queue.put_nowait(pil_image)

    def get_latest_depth(self) -> Optional[np.ndarray]:
        """
        Return the most recently computed depth map, or None if not yet available.

        Args:
            None

        Returns:
            Copy of the latest H×W depth array (values in [0, 1]), or None.
        """
        with self._lock:
            return self._latest_depth.copy() if self._latest_depth is not None else None

    def get_last_compute_ms(self) -> Optional[float]:
        """
        Return the most recent MiDaS forward time in milliseconds.

        This is the *actual* depth-model compute cost (measured on the worker
        thread), not the near-zero request-path submit/fetch overhead.

        Returns:
            Last MiDaS inference time in ms, or None if no depth computed yet.
        """
        with self._lock:
            return self._last_compute_ms

    def _worker(self) -> None:
        """
        Background thread: dequeues images and runs MiDaS inference continuously.

        Args:
            None

        Returns:
            None
        """
        while True:
            pil_image = self._queue.get()
            compute_start = time.perf_counter()
            depth = estimate_depth_map(pil_image)
            compute_ms = (time.perf_counter() - compute_start) * 1000.0
            with self._lock:
                self._latest_depth = depth
                self._last_compute_ms = compute_ms


depth_estimator: Optional[DepthEstimator] = None
if depth_model is not None:
    depth_estimator = DepthEstimator(depth_model, depth_transform)
    logger.info("Async depth estimator started.")

# ======================
#  TEMPORAL SMOOTHING STATE (per session)
# ======================

# YOLO/ResNet models are not thread-safe; Flask's dev server is threaded by
# default, so all model calls are serialized behind this lock.
_inference_lock = threading.Lock()
# Guards the per-session smoothing store below.
_smoothing_lock = threading.Lock()
# Debug frame-save counter (rolling buffer of recent frames).
_frame_counter = itertools.count()

# A session that hasn't sent a frame for this long is evicted (memory bound).
SESSION_IDLE_EVICT_SECONDS: float = 600.0


class _SessionSmoothing:
    """Sticky-hysteresis smoothing evidence for one client session."""

    __slots__ = ("scores", "committed", "last_seen")

    def __init__(self) -> None:
        self.scores: dict[str, float] = {}
        self.committed: str = "unknown"
        self.last_seen: float = time.time()


_sessions: dict[str, _SessionSmoothing] = {}


def _get_session(session_id: str) -> _SessionSmoothing:
    """
    Return (creating if needed) the smoothing state for a session, evicting idle ones.

    Caller must hold _smoothing_lock.

    Args:
        session_id: Client-provided session identifier ("default" if absent).

    Returns:
        The _SessionSmoothing record for this session, last_seen refreshed.
    """
    now = time.time()
    stale = [k for k, s in _sessions.items() if now - s.last_seen > SESSION_IDLE_EVICT_SECONDS]
    for k in stale:
        del _sessions[k]
    sess = _sessions.get(session_id)
    if sess is None:
        sess = _SessionSmoothing()
        _sessions[session_id] = sess
    sess.last_seen = now
    return sess

def classify_scene_resnet(pil_image: Image.Image) -> tuple[str, float]:
    """
    Run ResNet-18 biome classifier on a PIL image.
    Returns (biome_label, confidence) or ("unknown", 0.0) if model not loaded.
    """
    if resnet_model is None:
        return "unknown", 0.0
    try:
        tensor = resnet_transform(pil_image.convert("RGB")).unsqueeze(0)
        with _inference_lock, torch.no_grad():
            logits = resnet_model(tensor)
            probs = torch.softmax(logits, dim=1)[0]
        top_prob, top_idx = torch.max(probs, dim=0)
        label = resnet_classes[int(top_idx)]
        return label, float(top_prob)
    except Exception as exc:
        logger.warning(f"ResNet inference failed: {exc}")
        return "unknown", 0.0


# ======================
#  BIOME CLASSIFICATION
# ======================


def biome_from_objects(objects: list[dict[str, Any]]) -> tuple[str, float]:
    """
    Classify the biome from YOLO-detected objects using confidence-weighted scoring.

    Scores each biome using a weight matrix scaled by detection confidence and
    normalized bounding box area. Applies diversity bonuses for co-occurring
    class combinations and an explicit mixed-biome score.

    Args:
        objects: List of detection dicts, each containing:
            'label' (str), 'confidence' (float), 'bbox' (list[float] xyxy normalized),
            'center' (list[float]).

    Returns:
        Tuple (biome_label, biome_confidence) where biome_label is one of
        'forest', 'grassland', 'rocky', 'mixed', 'unknown' and biome_confidence
        is softmax-normalized in [0.0, 1.0].
    """
    if not objects:
        return "unknown", 0.0

    biome_scores: dict[str, float] = {
        "forest": 0.0, "grassland": 0.0, "rocky": 0.0, "mixed": 0.0,
    }
    detected_labels: set[str] = set()
    vegetation_score: float = 0.0
    rocky_score: float = 0.0

    for obj in objects:
        label = obj["label"].lower()
        conf = float(obj["confidence"])
        bbox = obj.get("bbox", [0.0, 0.0, 1.0, 1.0])
        area = max(0.0, (bbox[2] - bbox[0]) * (bbox[3] - bbox[1]))
        # sqrt dampens extremes: small objects still contribute meaningfully
        spatial_multiplier = area ** 0.5 if area > 0 else 1.0
        effective = conf * spatial_multiplier
        detected_labels.add(label)

        for biome, weight in BIOME_WEIGHTS.get(label, {}).items():
            biome_scores[biome] += weight * effective

        if label in VEGETATION_CLASSES:
            vegetation_score += effective
        if label in ROCKY_CLASSES:
            rocky_score += effective

    if len(detected_labels & FOREST_DIVERSITY_CLASSES) >= 2:
        biome_scores["forest"] += DIVERSITY_BONUS_FOREST
        logger.debug(f"Forest diversity bonus: {detected_labels & FOREST_DIVERSITY_CLASSES}")

    # (stone sınıfı kaldırıldı; rocky diversity bonus artık uygulanmıyor.)

    if vegetation_score > 0 and rocky_score > 0:
        biome_scores["mixed"] += min(vegetation_score, rocky_score) * MIXED_SCORE_MULTIPLIER

    max_score = max(biome_scores.values())
    if max_score < BIOME_UNKNOWN_THRESHOLD:
        logger.debug(f"Max biome score {max_score:.3f} below threshold — returning unknown")
        return "unknown", 0.0

    positive_sum = sum(s for s in biome_scores.values() if s > 0)
    biome_confidence = min(1.0, max_score / positive_sum) if positive_sum > 0 else 0.0
    best_biome = max(biome_scores, key=lambda b: biome_scores[b])
    logger.info(f"Biome scores: {biome_scores} -> '{best_biome}' (conf={biome_confidence:.3f})")
    return best_biome, biome_confidence


def smooth_biome(raw_biome: str, confidence: float, session_id: str = "default") -> str:
    """
    Stabilize biome predictions with confidence-weighted, sticky (hysteresis) smoothing.

    Each frame decays accumulated per-biome evidence and adds the current frame's
    confidence to its biome. The committed biome only changes when a challenger's
    evidence beats it by SWITCH_MARGIN, and is released to 'unknown' when evidence fades.
    This resists single-frame flips while still adapting to a sustained scene change.

    State is kept PER SESSION so multiple devices never contaminate each other's
    smoothing buffers. Thread-safe.

    Args:
        raw_biome: Biome label for the current frame.
        confidence: Confidence in [0, 1] for this frame's biome.
        session_id: Client session identifier (one per app launch on the device).

    Returns:
        The committed (smoothed) biome label for this session.
    """
    if not TEMPORAL_SMOOTHING_ENABLED:
        return raw_biome

    with _smoothing_lock:
        sess = _get_session(session_id)
        scores = sess.scores

        # Decay accumulated evidence each frame.
        for b in list(scores):
            scores[b] *= SMOOTHING_DECAY
            if scores[b] < 1e-3:
                del scores[b]

        # Accumulate this frame's evidence ('unknown' just lets the others decay).
        if raw_biome and raw_biome != "unknown" and confidence > 0:
            scores[raw_biome] = scores.get(raw_biome, 0.0) + float(confidence)

        if not scores:
            sess.committed = "unknown"
            return sess.committed

        leader = max(scores, key=scores.get)
        leader_score = scores[leader]
        held_score = scores.get(sess.committed, 0.0)

        if sess.committed == "unknown" or sess.committed not in scores:
            if leader_score >= RELEASE_FLOOR:
                sess.committed = leader
        elif leader != sess.committed and leader_score >= held_score * SWITCH_MARGIN:
            sess.committed = leader
        elif held_score < RELEASE_FLOOR:
            sess.committed = leader if leader_score >= RELEASE_FLOOR else "unknown"

        return sess.committed


# ======================
#  CLASS-AWARE NMS
# ======================


def apply_class_aware_nms(
    boxes: list[dict[str, Any]],
    class_iou_thresholds: dict[str, float],
) -> list[dict[str, Any]]:
    """
    Apply per-class NMS with class-specific IoU thresholds using torchvision.

    Groups detections by class, applies NMS independently per group using
    torchvision.ops.nms, then merges results.

    Args:
        boxes: Detection dicts with 'label', 'confidence', 'bbox' (normalized xyxy).
        class_iou_thresholds: Maps class name to IoU suppression threshold.

    Returns:
        Filtered list of detections after class-aware NMS.
    """
    if not boxes:
        return []

    by_class: dict[str, list[dict[str, Any]]] = {}
    for box in boxes:
        by_class.setdefault(box["label"], []).append(box)

    result: list[dict[str, Any]] = []
    for label, class_boxes in by_class.items():
        iou_thr = class_iou_thresholds.get(label, 0.45)
        box_tensor = torch.tensor(
            [b["bbox"] for b in class_boxes], dtype=torch.float32
        )
        score_tensor = torch.tensor(
            [b["confidence"] for b in class_boxes], dtype=torch.float32
        )
        keep = tv_nms(box_tensor, score_tensor, iou_thr)
        for idx in keep.tolist():
            result.append(class_boxes[idx])

    return result


# ======================
#  HEALTH CHECK
# ======================


@app.route("/health", methods=["GET"])
def health():
    return jsonify({
        "status": "ok",
        "yolo_weights": YOLO_WEIGHTS,
        "thresholds_file": str(CALIBRATED_THRESHOLDS_PATH),
        "class_thresholds": CLASS_CONFIDENCE_THRESHOLDS,
    }), 200


# ======================
#  METRICS
# ======================


@app.route("/metrics", methods=["GET"])
def metrics_endpoint():
    """
    Return aggregate latency/depth metrics over the recent request window.

    Use this to collect measured success-criteria evidence: after a real test
    session, GET /metrics gives average / p50 / p95 / p99 latency, throughput
    (fps), the share of requests under the 100 ms target, MiDaS depth coverage
    and the depth-value distribution, plus a biome distribution.

    Query params:
        reset=true — clear the rolling window to start a fresh measurement
            session; returns {"status": "reset"} instead of the summary.

    Returns:
        JSON metrics summary (HTTP 200). See MetricsCollector.summary().
    """
    if request.args.get("reset", "").lower() == "true":
        metrics.reset()
        return jsonify({"status": "reset"}), 200
    return jsonify(metrics.summary()), 200


# ======================
#  BIOME + OBJECT ANALYSIS
# ======================


@app.route("/analyze_frame", methods=["POST"])
def analyze_frame():
    """
    Receive a camera frame from Unity and return YOLO detections with biome and depth.

    Each detected object now includes a 'depth' field (0.0=far, 1.0=very close).
    Depth is computed with MiDaS (async) when available, otherwise via bbox heuristics.

    Args:
        (HTTP) image: Multipart/form-data file field containing PNG or JPEG.

    Returns:
        JSON with 'biome', 'biome_confidence', 'objects' (including depth),
        and 'debug' (timing, raw_biome, depth_method).
        HTTP 400 on missing/invalid/out-of-range image.
    """
    request_start = time.perf_counter()

    if "image" not in request.files:
        return jsonify({"error": "no image field"}), 400

    # Per-device smoothing isolation. Unity sends a fresh GUID per app launch.
    session_id = request.form.get("session_id", "default")

    img_bytes = request.files["image"].read()
    try:
        pil_img = Image.open(io.BytesIO(img_bytes)).convert("RGB")
    except Exception as exc:
        logger.warning(f"Failed to decode image: {exc}")
        return jsonify({"error": "invalid image data"}), 400

    img_w, img_h = pil_img.size
    if img_w < MIN_IMAGE_DIM or img_h < MIN_IMAGE_DIM:
        return jsonify({
            "error": (
                f"Image too small: {img_w}x{img_h}. "
                f"Minimum is {MIN_IMAGE_DIM}x{MIN_IMAGE_DIM}."
            )
        }), 400
    if img_w > MAX_IMAGE_DIM or img_h > MAX_IMAGE_DIM:
        return jsonify({
            "error": (
                f"Image too large: {img_w}x{img_h}. "
                f"Maximum is {MAX_IMAGE_DIM}x{MAX_IMAGE_DIM}."
            )
        }), 400

    if DEBUG_SAVE_FRAMES:
        DEBUG_FRAMES_DIR.mkdir(exist_ok=True)
        pil_img.save(
            DEBUG_FRAMES_DIR / f"frame_{next(_frame_counter) % DEBUG_FRAMES_KEEP:03d}.jpg",
            "JPEG", quality=90,
        )

    # --- YOLO: raw detections with NMS disabled ---
    inference_start = time.perf_counter()
    with _inference_lock:
        results = yolo_model(
            pil_img,
            imgsz=YOLO_IMGSZ,
            conf=RAW_CONF_THRESHOLD,
            iou=RAW_IOU_THRESHOLD,
            verbose=False,
        )
    inference_ms = (time.perf_counter() - inference_start) * 1000

    r = results[0]
    raw_boxes: list[dict[str, Any]] = []
    filtered_out: dict[str, list[float]] = {}

    for box in r.boxes:
        cls_id = int(box.cls[0])
        label = r.names[cls_id]
        conf = float(box.conf[0])

        threshold = CLASS_CONFIDENCE_THRESHOLDS.get(label, 0.50)
        if conf < threshold:
            filtered_out.setdefault(label, []).append(conf)
            logger.debug(f"Filtered {label} conf={conf:.3f} < threshold={threshold}")
            continue

        x1, y1, x2, y2 = box.xyxy[0].tolist()
        nx1, ny1, nx2, ny2 = x1 / img_w, y1 / img_h, x2 / img_w, y2 / img_h
        raw_boxes.append({
            "label": label,
            "confidence": conf,
            "bbox": [nx1, ny1, nx2, ny2],
            "center": [(nx1 + nx2) / 2.0, (ny1 + ny2) / 2.0],
        })

    # --- Class-aware NMS ---
    objects: list[dict[str, Any]] = apply_class_aware_nms(raw_boxes, CLASS_IOU_THRESHOLDS)
    detected_classes: dict[str, int] = {}
    for obj in objects:
        detected_classes[obj["label"]] = detected_classes.get(obj["label"], 0) + 1

    logger.debug(f"After NMS: {detected_classes}, filtered_out: {filtered_out}")

    # --- Async depth estimation ---
    depth_start = time.perf_counter()
    depth_method = "heuristic"
    depth_map: Optional[np.ndarray] = None

    if depth_estimator is not None:
        depth_estimator.submit(pil_img)
        depth_map = depth_estimator.get_latest_depth()
        depth_method = "midas_async" if depth_map is not None else "heuristic"

    depth_inference_ms = (time.perf_counter() - depth_start) * 1000

    # --- Attach depth to each object ---
    for obj in objects:
        if depth_map is not None:
            depth_val = get_object_depth(depth_map, obj["bbox"], (img_w, img_h))
        else:
            depth_val = estimate_depth_heuristic(obj["bbox"], obj["label"], img_h)
        obj["depth"] = round(depth_val, 4)
        obj["depth_normalized"] = round(depth_val, 4)

    # --- Biome decision (ResNet primary, rule-based fallback) ---
    resnet_start = time.perf_counter()
    resnet_biome, resnet_conf = classify_scene_resnet(pil_img)
    resnet_ms = (time.perf_counter() - resnet_start) * 1000
    rule_biome, rule_confidence = biome_from_objects(objects)

    # ResNet only overrides the object-grounded rules when it is very confident AND not
    # contradicted by the detected objects (or the rules saw nothing). Otherwise the
    # interpretable rule result wins — this stops the 3-way softmax from forcing a biome.
    RESNET_THRESHOLD = 0.90
    if (resnet_model is not None
            and resnet_conf >= RESNET_THRESHOLD
            and (resnet_biome == rule_biome or rule_biome == "unknown")):
        raw_biome = resnet_biome
        biome_confidence = max(resnet_conf, rule_confidence)
    else:
        raw_biome = rule_biome
        biome_confidence = rule_confidence
    # A clearly mixed scene from the rules always wins.
    if rule_biome == "mixed":
        raw_biome = "mixed"
        biome_confidence = rule_confidence

    biome = smooth_biome(raw_biome, biome_confidence, session_id)

    total_request_ms = (time.perf_counter() - request_start) * 1000

    # Real MiDaS forward time (measured on the worker thread); None when this
    # frame fell back to the geometric heuristic.
    depth_compute_ms = (
        depth_estimator.get_last_compute_ms()
        if (depth_estimator is not None and depth_method == "midas_async")
        else None
    )
    metrics.record(
        total_ms=total_request_ms,
        yolo_ms=inference_ms,
        resnet_ms=resnet_ms,
        depth_ms=depth_compute_ms,
        n_objects=len(objects),
        depth_method=depth_method,
        biome=biome,
        depth_values=[obj["depth"] for obj in objects],
    )

    logger.info(
        f"[{session_id[:8]}] biome='{biome}' raw='{raw_biome}' "
        f"resnet='{resnet_biome}'({resnet_conf:.2f}) rules='{rule_biome}'({rule_confidence:.2f}) "
        f"objs={len(objects)} {total_request_ms:.0f}ms"
    )

    return jsonify({
        "biome": biome,
        "biome_confidence": round(biome_confidence, 4),
        "objects": objects,
        "debug": {
            "detected_classes": detected_classes,
            "total_objects": len(objects),
            "inference_ms": round(inference_ms, 2),
            "resnet_ms": round(resnet_ms, 2),
            "depth_inference_ms": round(depth_inference_ms, 2),
            "depth_compute_ms": round(depth_compute_ms, 2) if depth_compute_ms is not None else None,
            "total_request_ms": round(total_request_ms, 2),
            "depth_method": depth_method,
            "raw_biome": raw_biome,
            "resnet_biome": resnet_biome,
            "resnet_confidence": round(resnet_conf, 4),
            "rule_biome": rule_biome,
            "session_id": session_id,
        },
    })


# ======================
#  DEPTH MAP VISUALIZATION (DEBUG)
# ======================


@app.route("/depth_map", methods=["POST"])
def depth_map_endpoint():
    """
    Debug endpoint: return a colorized depth map image as JPEG.

    Accepts the same multipart/form-data image input as /analyze_frame.
    Bright colors (plasma colormap) indicate objects close to the camera.

    Args:
        (HTTP) image: Multipart/form-data file field containing PNG or JPEG.

    Returns:
        JPEG image response with colorized depth map.
        HTTP 400 if image missing. HTTP 503 if depth model not loaded.
    """
    if "image" not in request.files:
        return jsonify({"error": "no image field"}), 400

    img_bytes = request.files["image"].read()
    try:
        pil_img = Image.open(io.BytesIO(img_bytes)).convert("RGB")
    except Exception as exc:
        return jsonify({"error": f"invalid image data: {exc}"}), 400

    depth_map = estimate_depth_map(pil_img)
    if depth_map is None:
        return jsonify({"error": "depth model not loaded — set DEPTH_ENABLED=true"}), 503

    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    fig, ax = plt.subplots(1, 1, figsize=(8, 6))
    ax.imshow(depth_map, cmap="plasma")
    ax.axis("off")
    ax.set_title("Depth Map (bright = close, dark = far)")

    buf = io.BytesIO()
    plt.savefig(buf, format="jpeg", bbox_inches="tight", dpi=100)
    plt.close(fig)
    buf.seek(0)

    return send_file(buf, mimetype="image/jpeg")


# ======================
#  /generate_mesh (DUMMY)
# ======================


@app.route("/generate_mesh", methods=["POST"])
def generate_mesh():
    """
    Placeholder mesh generation endpoint — returns a sample GLB URL.

    Args:
        (HTTP) image: Optional multipart/form-data file field (logged, not processed).

    Returns:
        JSON with 'model_url' (str) and 'format' (str) fields.
    """
    if "image" in request.files:
        img_bytes = request.files["image"].read()
        logger.info(f"[generate_mesh] received image: {len(img_bytes)} bytes")
    else:
        logger.info("[generate_mesh] no image field received")

    model_url = (
        "https://github.com/KhronosGroup/glTF-Sample-Models/raw/master/2.0/"
        "Box/glTF-Binary/Box.glb"
    )
    logger.info(f"[generate_mesh] returning model_url={model_url}")
    return jsonify({"model_url": model_url, "format": "glb"})


if __name__ == "__main__":
    port = int(os.getenv("PORT", "5001"))
    logger.info(f"Starting MAIN server on port {port}...")
    app.run(host="0.0.0.0", port=port, debug=False)
