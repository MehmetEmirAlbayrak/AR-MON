"""Integration tests for the AR-MON Flask server using pytest and Flask test client."""

import io

import numpy as np
import pytest
from PIL import Image

from server import app


# ========================
#  FIXTURES & HELPERS
# ========================


@pytest.fixture
def client():
    """Provide a Flask test client with TESTING mode enabled."""
    app.config["TESTING"] = True
    with app.test_client() as client:
        yield client


def make_test_image(
    width: int = 640,
    height: int = 480,
    color: tuple[int, int, int] = (34, 139, 34),
) -> bytes:
    """
    Create a solid-color test image encoded as JPEG bytes.

    Args:
        width: Image width in pixels.
        height: Image height in pixels.
        color: RGB tuple for the fill color.

    Returns:
        JPEG-encoded image as bytes.
    """
    arr = np.full((height, width, 3), color, dtype=np.uint8)
    img = Image.fromarray(arr)
    buf = io.BytesIO()
    img.save(buf, format="JPEG")
    return buf.getvalue()


# ========================
#  HEALTH ENDPOINT
# ========================


class TestHealthEndpoint:
    def test_health_returns_ok(self, client):
        response = client.get("/health")
        assert response.status_code == 200
        assert response.json["status"] == "ok"

    def test_health_method_get_only(self, client):
        response = client.post("/health")
        assert response.status_code == 405


# ========================
#  ANALYZE_FRAME ENDPOINT
# ========================


class TestAnalyzeFrame:
    def test_valid_image_returns_200(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        assert response.status_code == 200

    def test_valid_image_returns_required_fields(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        body = response.json
        assert "biome" in body
        assert "biome_confidence" in body
        assert "objects" in body
        assert "debug" in body

    def test_debug_contains_timing_fields(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        debug = response.json["debug"]
        assert "inference_ms" in debug
        assert "total_request_ms" in debug
        assert "raw_biome" in debug
        assert "depth_method" in debug

    def test_objects_have_required_fields(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        for obj in response.json["objects"]:
            assert "label" in obj
            assert "confidence" in obj
            assert "bbox" in obj
            assert "center" in obj

    def test_objects_have_depth_field(self, client):
        """Each detected object must carry a 'depth' value in [0, 1]."""
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        for obj in response.json["objects"]:
            assert "depth" in obj, f"Object missing 'depth': {obj}"
            assert 0.0 <= obj["depth"] <= 1.0

    def test_biome_confidence_range(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        conf = response.json["biome_confidence"]
        assert 0.0 <= conf <= 1.0

    def test_biome_is_valid_value(self, client):
        valid_biomes = {"forest", "grassland", "rocky", "mixed", "unknown"}
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        assert response.json["biome"] in valid_biomes

    def test_missing_image_returns_400(self, client):
        response = client.post(
            "/analyze_frame", data={}, content_type="multipart/form-data"
        )
        assert response.status_code == 400
        assert "error" in response.json

    def test_too_small_image_returns_400(self, client):
        tiny = make_test_image(width=32, height=32)
        data = {"image": (io.BytesIO(tiny), "tiny.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        assert response.status_code == 400
        assert "too small" in response.json["error"].lower()

    def test_too_large_image_returns_400(self, client):
        # Build a minimal oversized image header without allocating full memory
        huge = make_test_image(width=4097, height=4097)
        data = {"image": (io.BytesIO(huge), "huge.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        assert response.status_code == 400
        assert "too large" in response.json["error"].lower()

    def test_invalid_image_data_returns_400(self, client):
        data = {"image": (io.BytesIO(b"not an image"), "bad.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        assert response.status_code == 400


# ========================
#  DEPTH ESTIMATION
# ========================


class TestDepthEstimation:
    def test_heuristic_depth_in_range(self):
        from server import estimate_depth_heuristic
        depth = estimate_depth_heuristic([0.1, 0.1, 0.4, 0.9], "tree", 640)
        assert 0.0 <= depth <= 1.0

    def test_larger_bbox_gives_higher_depth(self):
        """Larger bounding box (closer object) should produce higher depth score."""
        from server import estimate_depth_heuristic
        depth_large = estimate_depth_heuristic([0.0, 0.0, 0.8, 0.8], "tree", 640)
        depth_small = estimate_depth_heuristic([0.4, 0.4, 0.5, 0.5], "tree", 640)
        assert depth_large > depth_small

    def test_lower_bbox_center_gives_higher_depth(self):
        """Object lower in frame (closer via perspective) should have higher depth."""
        from server import estimate_depth_heuristic
        # Same size, different vertical position
        depth_low = estimate_depth_heuristic([0.3, 0.6, 0.7, 0.9], "rock", 640)
        depth_high = estimate_depth_heuristic([0.3, 0.0, 0.7, 0.3], "rock", 640)
        assert depth_low > depth_high

    def test_heuristic_all_classes(self):
        from server import estimate_depth_heuristic
        for label in ("tree", "bush", "grass", "leaf", "rock", "stone"):
            depth = estimate_depth_heuristic([0.2, 0.2, 0.6, 0.8], label, 480)
            assert 0.0 <= depth <= 1.0, f"Depth out of range for label '{label}'"

    def test_get_object_depth_returns_float(self):
        from server import get_object_depth
        dummy_map = np.random.rand(480, 640).astype(np.float32)
        depth = get_object_depth(dummy_map, [0.1, 0.1, 0.5, 0.9], (640, 480))
        assert isinstance(depth, float)
        assert 0.0 <= depth <= 1.0

    def test_get_object_depth_empty_region_fallback(self):
        """A zero-area bbox should return the fallback value 0.5."""
        from server import get_object_depth
        dummy_map = np.random.rand(480, 640).astype(np.float32)
        # Bbox with no pixels (x1==x2)
        depth = get_object_depth(dummy_map, [0.5, 0.5, 0.5, 0.5], (640, 480))
        assert depth == 0.5


# ========================
#  CLASS-AWARE NMS
# ========================


class TestClassAwareNms:
    def test_nms_removes_high_overlap_boxes(self):
        from server import apply_class_aware_nms, CLASS_IOU_THRESHOLDS
        # Two almost identical boxes of the same class
        boxes = [
            {"label": "tree", "confidence": 0.90, "bbox": [0.1, 0.1, 0.5, 0.9],
             "center": [0.3, 0.5]},
            {"label": "tree", "confidence": 0.75, "bbox": [0.11, 0.11, 0.51, 0.91],
             "center": [0.31, 0.51]},
        ]
        result = apply_class_aware_nms(boxes, CLASS_IOU_THRESHOLDS)
        assert len(result) == 1
        assert result[0]["confidence"] == 0.90

    def test_nms_keeps_non_overlapping_boxes(self):
        from server import apply_class_aware_nms, CLASS_IOU_THRESHOLDS
        boxes = [
            {"label": "rock", "confidence": 0.85, "bbox": [0.0, 0.0, 0.2, 0.2],
             "center": [0.1, 0.1]},
            {"label": "rock", "confidence": 0.80, "bbox": [0.8, 0.8, 1.0, 1.0],
             "center": [0.9, 0.9]},
        ]
        result = apply_class_aware_nms(boxes, CLASS_IOU_THRESHOLDS)
        assert len(result) == 2

    def test_nms_empty_input(self):
        from server import apply_class_aware_nms, CLASS_IOU_THRESHOLDS
        assert apply_class_aware_nms([], CLASS_IOU_THRESHOLDS) == []


# ========================
#  BIOME SCORING
# ########################


class TestSessionSmoothing:
    def setup_method(self):
        from server import _sessions
        _sessions.clear()

    def test_sessions_are_isolated(self):
        """Evidence accumulated in one session must never leak into another."""
        from server import smooth_biome
        for _ in range(5):
            a = smooth_biome("forest", 0.9, "session_a")
        b = smooth_biome("rocky", 0.9, "session_b")
        assert a == "forest"
        assert b in ("rocky", "unknown")  # never 'forest' — that would be leakage
        assert b != "forest"

    def test_sticky_biome_resists_single_flip(self):
        from server import smooth_biome
        for _ in range(5):
            committed = smooth_biome("forest", 0.9, "session_c")
        # One contradictory frame must not flip the committed biome
        committed = smooth_biome("rocky", 0.9, "session_c")
        assert committed == "forest"

    def test_idle_sessions_evicted(self):
        from server import smooth_biome, _sessions, SESSION_IDLE_EVICT_SECONDS
        smooth_biome("forest", 0.9, "old_session")
        _sessions["old_session"].last_seen -= SESSION_IDLE_EVICT_SECONDS + 1
        smooth_biome("rocky", 0.9, "new_session")
        assert "old_session" not in _sessions
        assert "new_session" in _sessions

    def test_analyze_frame_accepts_session_id_field(self, client=None):
        from server import app
        app.config["TESTING"] = True
        with app.test_client() as c:
            data = {
                "image": (io.BytesIO(make_test_image()), "test.jpg"),
                "session_id": "pytest-session",
            }
            response = c.post(
                "/analyze_frame", data=data, content_type="multipart/form-data"
            )
            assert response.status_code == 200
            assert response.json["debug"]["session_id"] == "pytest-session"


class TestBiomeScoring:
    def test_empty_objects_returns_unknown(self):
        from server import biome_from_objects
        biome, conf = biome_from_objects([])
        assert biome == "unknown"
        assert conf == 0.0

    def test_trees_give_forest(self):
        from server import biome_from_objects
        objects = [
            {"label": "tree", "confidence": 0.90, "bbox": [0.0, 0.0, 0.5, 1.0],
             "center": [0.25, 0.5]},
            {"label": "leaf", "confidence": 0.85, "bbox": [0.5, 0.0, 1.0, 1.0],
             "center": [0.75, 0.5]},
        ]
        biome, conf = biome_from_objects(objects)
        assert biome == "forest"
        assert conf > 0.0

    def test_rocks_give_rocky(self):
        from server import biome_from_objects
        objects = [
            {"label": "rock", "confidence": 0.88, "bbox": [0.1, 0.1, 0.4, 0.6],
             "center": [0.25, 0.35]},
            {"label": "stone", "confidence": 0.82, "bbox": [0.5, 0.2, 0.9, 0.7],
             "center": [0.7, 0.45]},
        ]
        biome, conf = biome_from_objects(objects)
        assert biome == "rocky"

    def test_confidence_in_range(self):
        from server import biome_from_objects
        objects = [
            {"label": "grass", "confidence": 0.75, "bbox": [0.0, 0.5, 1.0, 1.0],
             "center": [0.5, 0.75]},
        ]
        _, conf = biome_from_objects(objects)
        assert 0.0 <= conf <= 1.0


# ========================
#  METRICS ENDPOINT
# ========================


class TestMetricsEndpoint:
    def test_metrics_returns_200_and_shape(self, client):
        response = client.get("/metrics")
        assert response.status_code == 200
        body = response.json
        for key in (
            "count", "latency_target_ms", "pct_under_target",
            "throughput_fps", "latency_ms", "depth", "biomes",
        ):
            assert key in body, f"missing key '{key}' in /metrics"
        assert "total" in body["latency_ms"]
        assert "depth" in body["latency_ms"]

    def test_analyze_frame_increments_metrics(self, client):
        client.get("/metrics?reset=true")  # clean slate
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        client.post("/analyze_frame", data=data, content_type="multipart/form-data")
        assert client.get("/metrics").json["count"] == 1

    def test_metrics_reset_clears_count(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        client.post("/analyze_frame", data=data, content_type="multipart/form-data")
        client.get("/metrics?reset=true")
        assert client.get("/metrics").json["count"] == 0

    def test_debug_has_resnet_and_depth_compute_ms(self, client):
        data = {"image": (io.BytesIO(make_test_image()), "test.jpg")}
        response = client.post(
            "/analyze_frame", data=data, content_type="multipart/form-data"
        )
        debug = response.json["debug"]
        assert "resnet_ms" in debug
        assert "depth_compute_ms" in debug
