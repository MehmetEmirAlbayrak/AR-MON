# AR Biome Detection Backend

Flask-based backend server for real-time object detection and biome classification in augmented reality mobile games. Uses custom-trained YOLOv8 models to detect environmental objects (bush, grass, leaf, rock, stone, tree) and classify biomes based on detected objects.

## 🎯 Features

- **Custom YOLOv8 Object Detection**: Trained on real-world photographs achieving 97.8% mAP50 accuracy
- **6 Object Classes**: Detects bush, grass, leaf, rock, stone, and tree
- **Hybrid Biome Classification**: Rule-based system classifying environments into forest, grassland, rocky, mixed, or unknown
- **Class-Specific Confidence Thresholds**: Optimized thresholds for each object type
- **RESTful API**: Flask-based endpoints for Unity mobile application integration
- **Fast Inference**: Average 45-55ms per frame on NVIDIA RTX 4060

## 📋 Requirements

- Python 3.10+
- CUDA-capable GPU (recommended) or CPU
- 8GB+ RAM
- ~2GB disk space for models

## 🚀 Installation

### 1. Clone the repository

```bash
git clone https://github.com/MehmetEmirAlbayrak/AR-MON.git
cd AR-MON/backend
```

### 2. Create virtual environment

```bash
python -m venv venv
```

### 3. Activate virtual environment

**Windows:**
```bash
venv\Scripts\activate
```

**Linux/Mac:**
```bash
source venv/bin/activate
```

### 4. Install dependencies

```bash
pip install -r requirements.txt
```

### 5. Download model weights

Place the trained YOLO model (`nature_custom.pt`) in the `models/` directory, or update the `YOLO_WEIGHTS` environment variable to point to your model file.

## 🏃 Running the Server

### Start the server

```bash
python server.py
```

The server will start on `http://0.0.0.0:5001` by default.

### Environment Variables

- `PORT`: Server port (default: 5001)
- `YOLO_WEIGHTS`: Path to YOLO model weights (default: `models/nature_custom.pt`)

Example:
```bash
set PORT=8080
set YOLO_WEIGHTS=models/best.pt
python server.py
```

## 📡 API Endpoints

### POST `/analyze_frame`

Analyzes a camera frame and returns detected objects with biome classification.

**Request:**
- Method: `POST`
- Content-Type: `multipart/form-data`
- Body: `image` field containing JPEG/PNG image

**Response:**
```json
{
    "biome": "forest",
    "objects": [
        {
            "label": "tree",
            "confidence": 0.85,
            "bbox": [0.12, 0.15, 0.45, 0.89],
            "center": [0.285, 0.52]
        },
        {
            "label": "grass",
            "confidence": 0.65,
            "bbox": [0.0, 0.7, 1.0, 1.0],
            "center": [0.5, 0.85]
        }
    ],
    "debug": {
        "detected_classes": {
            "tree": 2,
            "grass": 1
        },
        "total_objects": 3
    }
}
```

**Biome Values:**
- `forest`: Detected trees, leaves, or bushes
- `grassland`: Primarily grass with minimal trees/rocks
- `rocky`: Primarily rocks/stones
- `mixed`: Combination of vegetation and rocks
- `unknown`: No objects detected or low confidence

### GET `/health`

Health check endpoint for monitoring server status.

**Response:**
```json
{
    "status": "ok"
}
```

## 🧠 Model Information

### Current Model: `nature_custom.pt`

- **Architecture**: YOLOv8n (YOLOv8 Nano)
- **Training Dataset**: Custom-captured real-world photographs (nature_yolo)
- **Classes**: 6 (bush, grass, leaf, rock, stone, tree)
- **Performance**:
  - mAP50: 97.8%
  - Precision: 97.2%
  - Recall: 95.3%
- **Per-Class Performance**:
  - bush: 99.5% mAP50
  - grass: 93.2% mAP50
  - leaf: 99.2% mAP50
  - rock: 99.5% mAP50
  - stone: 99.5% mAP50
  - tree: 95.8% mAP50

### Class-Specific Confidence Thresholds

The server uses different confidence thresholds for each class to optimize detection:

| Class | Threshold | Reason |
|-------|-----------|--------|
| tree | 0.70 | High detection accuracy |
| leaf | 0.70 | High detection accuracy |
| grass | 0.40 | Lower threshold for better detection |
| bush | 0.40 | Lower threshold for better detection |
| rock | 0.50 | Moderate threshold |
| stone | 0.50 | Moderate threshold |

## 🔧 Configuration

### Adjusting Confidence Thresholds

Edit the `CLASS_CONFIDENCE_THRESHOLDS` dictionary in `server.py`:

```python
CLASS_CONFIDENCE_THRESHOLDS = {
    "tree": 0.70,
    "leaf": 0.70,
    "grass": 0.40,  # Lower for better detection
    "bush": 0.40,   # Lower for better detection
    "rock": 0.50,
    "stone": 0.50,
}
```

### Biome Classification Rules

The biome classification logic can be customized in the `biome_from_objects()` function in `server.py`.

## 📊 Training Models

### Dataset Preparation

Use the provided scripts to prepare datasets:

```bash
# Merge multiple datasets
python merge_datasets_v2.py

# Prepare nature_yolo dataset
python prepare_nature_dataset.py
```

### Training a New Model

```bash
yolo detect train data=nature_yolo/data.yaml model=yolov8n.pt epochs=150 imgsz=640
```

## 🐛 Debugging

The server includes debug output showing:
- Detected classes and their confidence scores
- Filtered out objects (below threshold)
- Total number of detected objects
- Final biome classification

Check the terminal output for debug information.

## 📁 Project Structure

```
Ar-Backend/
├── server.py                 # Main Flask server
├── requirements.txt          # Python dependencies
├── models/                   # Trained YOLO models
│   └── nature_custom.pt
├── nature_yolo/             # Custom dataset
│   ├── train/
│   ├── val/
│   └── test/
├── merge_datasets_v2.py     # Dataset merging script
├── prepare_nature_dataset.py # Dataset preparation script
└── README.md                # This file
```

## 🤝 Contributing

This project is part of a graduation thesis. For questions or contributions, please contact the repository owner.

## 📄 License

See LICENSE file for details.

## 🔗 Related Projects

- **Unity Mobile Application**: [AR-MON Unity Branch](https://github.com/MehmetEmirAlbayrak/AR-MON/tree/unity)
- **Graduation Thesis**: See `Graduation Project - Latex Template (2)/` directory

## 📞 Contact

- **Author**: Mehmet Emir Albayrak
- **University**: Gebze Technical University
- **Supervisor**: Prof. Dr. Yakup Genç

## 🙏 Acknowledgments

- Ultralytics for YOLOv8
- Roboflow for dataset management
- Unity AR Foundation for mobile AR integration
