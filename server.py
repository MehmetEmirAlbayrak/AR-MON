from flask import Flask, request, jsonify

app = Flask(__name__)

# Sadece Unity'den gelen isteği alıp hazır bir GLB linki döndürüyoruz.
# Şimdilik YOLO, sahne analizi, TripoSR vs. YOK – sadece prototip.

@app.route("/generate_mesh", methods=["POST"])
def generate_mesh():
    if "image" not in request.files:
        return jsonify({"error": "no image field"}), 400

    file = request.files["image"]
    img_bytes = file.read()

    print(f"[generate_mesh] received image bytes: {len(img_bytes)}")
    print(f"[Dummy] Pretending to analyze image of size {len(img_bytes)} bytes")

    # Şimdilik hep aynı GLB’yi döndürüyoruz (Damaged Helmet)
    model_url = "https://github.com/KhronosGroup/glTF-Sample-Models/raw/master/2.0/Box/glTF-Binary/Box.glb"

    print(f"[generate_mesh] returning model_url = {model_url}")

    return jsonify({
        "model_url": model_url,
        "format": "glb",
    })


if __name__ == "__main__":
    print("Starting MAIN server...")
    app.run(host="0.0.0.0", port=5001, debug=True)
