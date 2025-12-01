from flask import Flask, request, jsonify
from ultralytics import YOLO
from PIL import Image
import io
import os
import requests

import torch
from torchvision.models import resnet18, ResNet18_Weights

app = Flask(__name__)

# =========================
#  MODELLERİ YÜKLE
# =========================

print("Loading YOLO model...")
# Proje klasöründe yolo11n.pt olduğunu varsayıyorum.
yolo_model = YOLO("yolo11n.pt")

print("Loading scene model (ResNet-18)...")
weights = ResNet18_Weights.DEFAULT
scene_model = resnet18(weights=weights)
scene_model.eval()
scene_preprocess = weights.transforms()
CATEGORIES = weights.meta["categories"]

# =========================
#  BİYOM / SCENE FONKSİYONLARI
# =========================

def classify_scene_from_pil(pil_image: Image.Image):
    """Return (scene_label, prob) using ResNet-18."""
    batch = scene_preprocess(pil_image).unsqueeze(0)
    with torch.no_grad():
        logits = scene_model(batch)
        probs = logits.softmax(dim=1)[0]
    top_prob, top_idx = torch.max(probs, dim=0)
    label = CATEGORIES[top_idx]
    return label, float(top_prob)


def biome_from_scene_label(label: str) -> str:
    """Map ImageNet-style scene label to coarse biome."""
    l = label.lower()
    if any(k in l for k in ["forest", "valley", "wood", "jungle", "rainforest"]):
        return "forest"
    if any(k in l for k in ["mountain", "volcano", "alp"]):
        return "mountain"
    if any(k in l for k in ["beach", "seashore", "lakeside", "sea", "ocean"]):
        return "water"
    if any(k in l for k in ["street", "streetcar", "downtown", "plaza", "market", "city"]):
        return "urban"
    if any(k in l for k in ["field", "pasture", "farm"]):
        return "grassland"
    return "unknown"

# =========================
#  GLB URL'LERİ
# =========================

TREE_GLB = (
    "https://raw.githubusercontent.com/KhronosGroup/"
    "glTF-Sample-Models/master/2.0/Tree/glTF-Binary/Tree.glb"
)

ROCK_GLB = (
    "https://raw.githubusercontent.com/KhronosGroup/"
    "glTF-Sample-Models/master/2.0/Rock/glTF-Binary/Rock.glb"
)

DEFAULT_GLB = (
    "https://raw.githubusercontent.com/KhronosGroup/"
    "glTF-Sample-Models/master/2.0/DamagedHelmet/glTF-Binary/DamagedHelmet.glb"
)

def choose_object_type_from_yolo_result(result) -> str:
    """
    YOLO result içinden en baskın objeyi seçip:
      - 'tree' veya 'plant' benzeri label -> 'tree'
      - 'rock', 'stone' benzeri label -> 'rock'
      - aksi halde 'unknown'
    """
    if result is None or result.boxes is None or len(result.boxes) == 0:
        return "unknown"

    # Güveni en yüksek kutuyu seçelim
    best = None
    best_conf = -1.0
    for box in result.boxes:
        conf = float(box.conf[0])
        if conf > best_conf:
            best_conf = conf
            best = box

    if best is None:
        return "unknown"

    cls_id = int(best.cls[0])
    label = result.names[cls_id].lower()
    print(f"[YOLO] best label={label}, conf={best_conf:.3f}")

    if any(k in label for k in ["tree", "plant", "bush", "vegetation"]):
        return "tree"
    if any(k in label for k in ["rock", "stone", "boulder"]):
        return "rock"

    return "unknown"


def glb_url_for_object_type(object_type: str) -> str:
    if object_type == "tree":
        return TREE_GLB
    if object_type == "rock":
        return ROCK_GLB
    return DEFAULT_GLB

# =========================
#  ENDPOINTLER
# =========================

@app.route("/analyze_frame", methods=["POST"])
def analyze_frame():
    """
    - image alır
    - sahne sınıflandırması (biome)
    - YOLO ile obje tespiti
    """
    if "image" not in request.files:
        return jsonify({"error": "no image field"}), 400

    file = request.files["image"]
    img_bytes = file.read()
    print(f"[analyze_frame] received image bytes: {len(img_bytes)}")

    pil_img = Image.open(io.BytesIO(img_bytes)).convert("RGB")

    # Sahne / biyom
    scene_label, scene_conf = classify_scene_from_pil(pil_img)
    biome = biome_from_scene_label(scene_label)

    # YOLO objeleri
    results = yolo_model(pil_img)
    r = results[0]
    w, h = pil_img.size

    objects = []
    for box in r.boxes:
        cls_id = int(box.cls[0])
        label = r.names[cls_id]
        conf = float(box.conf[0])
        x1, y1, x2, y2 = box.xyxy[0].tolist()
        nx1, ny1, nx2, ny2 = x1 / w, y1 / h, x2 / w, y2 / h
        cx = (nx1 + nx2) / 2.0
        cy = (ny1 + ny2) / 2.0

        objects.append({
            "label": label,
            "confidence": conf,
            "bbox": [nx1, ny1, nx2, ny2],
            "center": [cx, cy],
        })

    return jsonify({
        "scene_label": scene_label,
        "scene_conf": scene_conf,
        "biome": biome,
        "objects": objects,
    })


@app.route("/generate_mesh", methods=["POST"])
def generate_mesh():
    """
    - `image` dosyasını alır (Unity'den screenshot)
    - YOLO ile bakıp object_type seçer (tree / rock / unknown)
    - Uygun GLB URL’ini döndürür
    """
    if "image" not in request.files:
        return jsonify({"error": "no image field"}), 400

    file = request.files["image"]
    img_bytes = file.read()
    print(f"[generate_mesh] received image bytes: {len(img_bytes)}")

    pil_img = Image.open(io.BytesIO(img_bytes)).convert("RGB")

    # YOLO ile tek kare çalıştır
    results = yolo_model(pil_img)
    r = results[0]

    object_type = choose_object_type_from_yolo_result(r)
    model_url = glb_url_for_object_type(object_type)

    print(f"[generate_mesh] object_type={object_type}, model_url={model_url}")

    return jsonify({
        "model_url": model_url,
        "object_type": object_type,
        "format": "glb",
    })


if __name__ == "__main__":
    print("Starting MAIN server...")
    # Railway PORT'u environment variable olarak veriyor
    port = int(os.environ.get("PORT", 5001))
    app.run(host="0.0.0.0", port=port, debug=False)

