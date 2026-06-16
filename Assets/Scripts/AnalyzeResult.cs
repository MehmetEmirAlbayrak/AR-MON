using System;

[Serializable]
public class AnalyzeResult
{
    public string scene_label;
    public float scene_conf;

    // Flask'tan gelen biome string'i
    public string biome;

    // Biome güven skoru (0-1) — server "biome_confidence" alanı
    public float biome_confidence;

    // List yerine dizi: JsonUtility için daha problemsiz
    public DetectedObject[] objects;
}

[Serializable]
public class DetectedObject
{
    // YOLO etiketi (tree, rock, grass vs.)
    public string label;

    // 0–1 arası güven skoru
    public float confidence;

    // [x1, y1, x2, y2] -> normalize bbox (0–1)
    public float[] bbox;

    // [cx, cy] -> normalize merkez (0–1)
    public float[] center;

    // MiDaS göreli derinlik: 0 = uzak, 1 = çok yakın. JsonUtility serverdan otomatik doldurur.
    public float depth;
    public float depth_normalized;

    // İstersen yardımcı property’ler:
    public float Width => bbox != null && bbox.Length == 4 ? Math.Abs(bbox[2] - bbox[0]) : 0f;
    public float Height => bbox != null && bbox.Length == 4 ? Math.Abs(bbox[3] - bbox[1]) : 0f;
}