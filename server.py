from flask import Flask, request, jsonify
from PIL import Image
import io
import os

import torch
from ultralytics import YOLO
from torchvision.models import resnet18, ResNet18_Weights

app = Flask(__name__)

# ======================
#  MODELLERİ YÜKLE
# ======================

print("Loading YOLO model...")
# YOLO ağırlık dosyan (repo kökünde olduğunu varsayıyorum)
YOLO_WEIGHTS = os.getenv("YOLO_WEIGHTS", "yolo11n.pt")
yolo_model = YOLO(YOLO_WEIGHTS)

print("Loading scene (biome) model...")
weights = ResNet18_Weights.DEFAULT
scene_model = resnet18(weights=weights)
scene_model.eval()
scene_preprocess = weights.transforms()
CATEGORIES = weights.meta["categories"]


def classify_scene_from_pil(pil_image: Image.Image):
    """ResNet-18 ile sahne etiketi döndürür (label, prob)."""
    batch = scene_preprocess(pil_image).unsqueeze(0)
    with torch.no_grad():
        logits = scene_model(batch)
        probs = logits.softmax(dim=1)[0]
    top_prob, top_idx = torch.max(probs, dim=0)
    label = CATEGORIES[top_idx]
    return label, float(top_prob)


def biome_from_scene_label(label: str) -> str:
    """
    ImageNet tarzı sahne etiketini kaba bir 'biome'a çevirir.
    Burayı istersen daha sonra genişletebiliriz.
    """
    l = label.lower()
    if any(k in l for k in ["forest", "valley", "wood", "jungle", "rainforest"]):
        return "forest"
    if any(k in l for k in ["mountain", "volcano", "alp", "cliff"]):
        return "mountain"
    if any(k in l for k in ["beach", "seashore", "lakeside", "sea", "ocean"]):
        return "water"
    if any(k in l for k in ["street", "streetcar", "downtown", "plaza", "market", "city"]):
        return "urban"
    if any(k in l for k in ["field", "pasture", "farm", "meadow"]):
        return "grassland"
    return "unknown"


# ======================
#   SAĞLIK KONTROLÜ
# ======================

@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok"}), 200


# ======================
#   BIOME + OBJE ANALİZİ
# ======================

@app.route("/analyze_frame", methods=["POST"])
def analyze_frame():
    """
    Unity'den gelen 'image' alanındaki PNG/JPEG'i alır:
      - ResNet ile sahne sınıflandırması -> biome
      - YOLO ile objeleri algılar -> bbox + label + confidence
    JSON döndürür.
    """
    if "image" not in request.files:
        return jsonify({"error": "no image field"}), 400

    file = request.files["image"]
    img_bytes = file.read()
    pil_img = Image.open(io.BytesIO(img_bytes)).convert("RGB")

    # --- 1) SAHNE / BIOME ---
    scene_label, scene_conf = classify_scene_from_pil(pil_img)
    biome = biome_from_scene_label(scene_label)

    # --- 2) YOLO OBJELER ---
    results = yolo_model(
        pil_img,
        imgsz=640,
        conf=0.35,
        verbose=False
    )
    r = results[0]
    w, h = pil_img.size

    objects = []
    for box in r.boxes:
        cls_id = int(box.cls[0])
        label = r.names[cls_id]
        conf = float(box.conf[0])
        x1, y1, x2, y2 = box.xyxy[0].tolist()

        # normalize
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


# ======================
#   ESKİ /generate_mesh
#   (şimdilik dummy bırakıyorum)
# ======================

@app.route("/generate_mesh", methods=["POST"])
def generate_mesh():
    """
    Şimdilik demoda GLB üretmiyoruz,
    her zaman aynı sample GLB linkini döndürüyoruz.
    İstersen tamamen silebilirsin.
    """
    # Debug için: görüntü kaç byte gelmiş görelim
    if "image" in request.files:
        img_bytes = request.files["image"].read()
        print(f"[generate_mesh] received image bytes: {len(img_bytes)}")
    else:
        print("[generate_mesh] no image field")

    # Buraya istediğin GLB URL'ini koy
    model_url = (
        "https://github.com/KhronosGroup/glTF-Sample-Models/raw/master/2.0/"
        "Box/glTF-Binary/Box.glb"
    )

    print(f"[generate_mesh] returning model_url = {model_url}")
    return jsonify({
        "model_url": model_url,
        "format": "glb",
    })


if __name__ == "__main__":
    # Railway'de PORT env var'ından okuyoruz
    port = int(os.getenv("PORT", "5001"))
    print(f"Starting MAIN server on port {port}...")
    app.run(host="0.0.0.0", port=port, debug=False)
