# AR Biome Detection Backend

Flask backend for real-time biome detection in an AR mobile game. It fuses three
models — a custom-trained **YOLOv8m** object detector (5 classes: bush, grass,
leaf, rock, tree), a **ResNet-18** scene classifier, and **MiDaS** monocular
depth — with confidence-weighted scoring and temporal smoothing, then serves the
result over HTTP to a Unity client.

## 🎯 Features

- **Custom YOLOv8m object detection** — 5 classes (bush, grass, leaf, rock, tree; stone merged into rock)
- **Hybrid biome classification** — YOLO + ResNet-18 + depth fused into forest / grassland / rocky / mixed / unknown
- **Per-class confidence thresholds** — calibrated offline, auto-loaded at startup
- **Temporal smoothing** — sticky hysteresis to keep the reported biome stable between frames
- **RESTful API** — endpoints for the Unity mobile client

## 📋 Requirements

- Python 3.10
- ~6 GB disk for dependencies + models (MiDaS is downloaded on first run via torch.hub)
- CUDA GPU recommended; runs on CPU

## 🚀 Installation

```bash
git clone https://github.com/MehmetEmirAlbayrak/AR-MON.git
cd AR-MON
git checkout backend

python -m venv venv
# Windows:  venv\Scripts\activate
# Linux/Mac: source venv/bin/activate

pip install -r requirements.txt
```

The trained weights ship in the `models/` directory — no extra download step
(MiDaS depth weights are fetched automatically on first run).

## 🏃 Running the Server

```bash
python server.py
```

Starts on `http://0.0.0.0:5001` by default.

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PORT` | `5001` | Server port |
| `YOLO_WEIGHTS` | `models/best.pt` | YOLO detector weights |
| `RESNET_WEIGHTS` | `models/biome_resnet18.pth` | ResNet-18 scene classifier weights |
| `RESNET_CLASSES` | `models/class_indices.json` | ResNet class-index map |
| `CALIBRATED_THRESHOLDS` | `calibrated_thresholds.json` | Per-class confidence thresholds |
| `DEPTH_ENABLED` | `1` | Enable MiDaS depth fusion |
| `TEMPORAL_SMOOTHING` | `1` | Enable sticky-hysteresis biome smoothing |

## 📡 API Endpoints

### POST `/analyze_frame`

Analyzes a camera frame and returns detected objects with biome classification.

- **Content-Type:** `multipart/form-data`
- **Body:** `image` field containing a JPEG/PNG image

```json
{
  "biome": "forest",
  "objects": [
    { "label": "tree", "confidence": 0.85, "bbox": [0.12, 0.15, 0.45, 0.89], "center": [0.285, 0.52] },
    { "label": "grass", "confidence": 0.65, "bbox": [0.0, 0.7, 1.0, 1.0], "center": [0.5, 0.85] }
  ],
  "debug": { "detected_classes": { "tree": 2, "grass": 1 }, "total_objects": 3 }
}
```

**Biome values:** `forest`, `grassland`, `rocky`, `mixed`, `unknown`.

### GET `/health`

Health check. Returns `{ "status": "ok" }`.

### GET `/metrics`

Rolling latency and depth-evidence aggregates.

## 🧠 Model Information

### YOLO detector — `models/best.pt`

- **Architecture:** YOLOv8m (~25M params)
- **Classes:** 5 (bush, grass, leaf, rock, tree)
- **Training data:** deduplicated, leak-free 70/20/10 splits with real-world
  rock supplementation and hard negatives (pavement/masonry) for the rock class

### Per-class confidence thresholds

Calibrated on the validation split and written to `calibrated_thresholds.json`,
which `server.py` auto-loads at startup:

| Class | Threshold |
|-------|-----------|
| tree  | 0.20 |
| leaf  | 0.30 |
| grass | 0.40 |
| bush  | 0.60 |
| rock  | 0.35 |

### ResNet-18 scene classifier — `models/biome_resnet18.pth`

Three-way scene classifier (forest / grassland / rocky) fused with the detector
output. Inference uses `Resize(256) → CenterCrop(224) → ToTensor()` only (no
ImageNet normalization), matching training.

### Depth — MiDaS (`MiDaS_small`)

Downloaded from torch.hub on first run; used to weight detections by estimated
proximity. Disable with `DEPTH_ENABLED=0`.

## 🧪 Tests

```bash
pytest -v
```

## 📁 Project Structure

```
AR-MON/  (backend branch)
├── server.py                  # Flask API + model fusion
├── metrics.py                 # Rolling metrics aggregator
├── requirements.txt           # Python dependencies
├── runtime.txt                # Python version pin
├── calibrated_thresholds.json # Per-class confidence thresholds
├── test_server.py             # API integration tests
├── test_metrics.py            # Metrics unit tests
└── models/
    ├── best.pt                # YOLOv8m detector
    ├── biome_resnet18.pth     # ResNet-18 scene classifier
    └── class_indices.json     # ResNet class-index map
```

## 🔗 Related

- **Unity client:** [AR-MON Unity branch](https://github.com/MehmetEmirAlbayrak/AR-MON/tree/unity)

## 📞 Contact

- **Author:** Mehmet Emir Albayrak
- **University:** Gebze Technical University
- **Supervisor:** Prof. Dr. Yakup Genç

## 🙏 Acknowledgments

- Ultralytics (YOLOv8) · Intel ISL (MiDaS) · Unity AR Foundation
