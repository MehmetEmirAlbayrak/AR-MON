import os
import glob
import shutil
import random

# --- AYARLAR --- #
BASE_DIR = r"C:\Users\Mehmet Emir\Ar-Backend"

# Tüm datasetler ve yeni class id'leri
# stone -> rock olarak birleştiriliyor (çok benzer)
DATASETS = [
    # (folder_name, class_mapping_dict)
    # class_mapping_dict: {eski_class_id: yeni_class_id}
    ("bush_yolo",   {0: 0}),           # bush -> 0
    ("grass_yolo",  {0: 1}),           # grass -> 1
    ("leaf_yolo",   {0: 2}),           # leaf -> 2
    ("rock_yolo",   {0: 3}),           # rock -> 3
    ("tree_yolo",   {0: 4}),           # tree -> 4
]

# nature_yolo özel - 6 sınıflı, mapping gerekli
# nature_yolo classes: ['bush', 'grass', 'leaf', 'rock', 'stone', 'tree']
#                       0       1        2       3       4        5
NATURE_YOLO_MAPPING = {
    0: 0,  # bush -> bush
    1: 1,  # grass -> grass
    2: 2,  # leaf -> leaf
    3: 3,  # rock -> rock
    4: 3,  # stone -> rock (birleştir)
    5: 4,  # tree -> tree
}

# Yeni sınıf isimleri
CLASS_NAMES = {
    0: "bush",
    1: "grass", 
    2: "leaf",
    3: "rock",
    4: "tree",
}

TARGET_NAME = "biome_merged_v2"
SPLITS = ["train", "val", "test"]
# Roboflow "valid" kullanıyor, biz "val" olarak kaydediyoruz
SPLIT_MAP = {"train": "train", "val": "valid", "test": "test"}

# nature_yolo sadece train var, %80 train, %15 val, %5 test olarak böl
NATURE_YOLO_SPLIT_RATIO = {"train": 0.80, "val": 0.15, "test": 0.05}

# -------------- #

target_root = os.path.join(BASE_DIR, TARGET_NAME)

# Hedef klasörleri oluştur
for split in SPLITS:
    os.makedirs(os.path.join(target_root, split, "images"), exist_ok=True)
    os.makedirs(os.path.join(target_root, split, "labels"), exist_ok=True)


def rewrite_labels_and_copy(
    src_root: str, dst_root: str, split: str, class_mapping: dict, prefix: str
):
    """Normal datasetler için - train/valid/test klasör yapısı var"""
    src_split = SPLIT_MAP.get(split, split)
    src_img_dir = os.path.join(src_root, src_split, "images")
    src_lbl_dir = os.path.join(src_root, src_split, "labels")
    
    if not os.path.exists(src_img_dir):
        print(f"  [SKIP] {prefix}/{src_split} bulunamadı")
        return 0

    dst_img_dir = os.path.join(dst_root, split, "images")
    dst_lbl_dir = os.path.join(dst_root, split, "labels")

    img_paths = glob.glob(os.path.join(src_img_dir, "*.*"))
    count = 0

    for img_path in img_paths:
        base = os.path.basename(img_path)
        name, ext = os.path.splitext(base)

        new_name = f"{prefix}_{name}{ext}"
        new_img_path = os.path.join(dst_img_dir, new_name)

        shutil.copy2(img_path, new_img_path)

        src_label_path = os.path.join(src_lbl_dir, f"{name}.txt")
        dst_label_path = os.path.join(dst_lbl_dir, f"{prefix}_{name}.txt")

        if not os.path.exists(src_label_path):
            continue

        with open(src_label_path, "r") as f_in, open(dst_label_path, "w") as f_out:
            for line in f_in:
                parts = line.strip().split()
                if len(parts) < 5:
                    continue
                old_class = int(parts[0])
                if old_class in class_mapping:
                    parts[0] = str(class_mapping[old_class])
                    f_out.write(" ".join(parts) + "\n")
        count += 1
    
    return count


def process_nature_yolo(src_root: str, dst_root: str, class_mapping: dict, prefix: str):
    """nature_yolo için - sadece train var, bölmemiz gerekiyor"""
    src_img_dir = os.path.join(src_root, "train", "images")
    src_lbl_dir = os.path.join(src_root, "train", "labels")
    
    if not os.path.exists(src_img_dir):
        print(f"  [SKIP] {prefix}/train bulunamadı")
        return {}

    img_paths = glob.glob(os.path.join(src_img_dir, "*.*"))
    random.shuffle(img_paths)
    
    total = len(img_paths)
    train_end = int(total * NATURE_YOLO_SPLIT_RATIO["train"])
    val_end = train_end + int(total * NATURE_YOLO_SPLIT_RATIO["val"])
    
    split_assignments = {
        "train": img_paths[:train_end],
        "val": img_paths[train_end:val_end],
        "test": img_paths[val_end:]
    }
    
    counts = {}
    for split, paths in split_assignments.items():
        dst_img_dir = os.path.join(dst_root, split, "images")
        dst_lbl_dir = os.path.join(dst_root, split, "labels")
        count = 0
        
        for img_path in paths:
            base = os.path.basename(img_path)
            name, ext = os.path.splitext(base)

            new_name = f"{prefix}_{name}{ext}"
            new_img_path = os.path.join(dst_img_dir, new_name)

            shutil.copy2(img_path, new_img_path)

            src_label_path = os.path.join(src_lbl_dir, f"{name}.txt")
            dst_label_path = os.path.join(dst_lbl_dir, f"{prefix}_{name}.txt")

            if not os.path.exists(src_label_path):
                continue

            with open(src_label_path, "r") as f_in, open(dst_label_path, "w") as f_out:
                for line in f_in:
                    parts = line.strip().split()
                    if len(parts) < 5:
                        continue
                    old_class = int(parts[0])
                    if old_class in class_mapping:
                        parts[0] = str(class_mapping[old_class])
                        f_out.write(" ".join(parts) + "\n")
            count += 1
        
        counts[split] = count
    
    return counts


if __name__ == "__main__":
    total_counts = {"train": 0, "val": 0, "test": 0}
    
    # Normal datasetleri işle
    for folder_name, class_mapping in DATASETS:
        src_root = os.path.join(BASE_DIR, folder_name)
        prefix = folder_name.split("_")[0]
        print(f"\n[{folder_name}] işleniyor...")

        for split in SPLITS:
            count = rewrite_labels_and_copy(
                src_root, target_root, split, class_mapping, prefix
            )
            total_counts[split] += count
            if count > 0:
                print(f"  {split}: {count} görüntü eklendi")
    
    # nature_yolo'yu işle (senin fotoğrafların)
    print(f"\n[nature_yolo] işleniyor (senin fotoğrafların)...")
    nature_src = os.path.join(BASE_DIR, "nature_yolo")
    nature_counts = process_nature_yolo(
        nature_src, target_root, NATURE_YOLO_MAPPING, "nature"
    )
    for split, count in nature_counts.items():
        total_counts[split] += count
        print(f"  {split}: {count} görüntü eklendi")

    # data.yaml oluştur
    yaml_path = os.path.join(target_root, "data.yaml")
    target_path_unix = target_root.replace("\\", "/")
    
    names_str = "\n".join([f"  {k}: {v}" for k, v in CLASS_NAMES.items()])
    
    yaml_text = f"""# Biome Merged Dataset v2
# Tüm sınıflar: bush, grass, leaf, rock, tree
# stone -> rock olarak birleştirildi

path: {target_path_unix}

train: train/images
val: val/images
test: test/images

nc: {len(CLASS_NAMES)}
names:
{names_str}
"""
    with open(yaml_path, "w", encoding="utf-8") as f:
        f.write(yaml_text)

    print("\n" + "="*50)
    print("✅ Birleştirme tamamlandı!")
    print("="*50)
    print(f"\n📁 Dataset: {target_root}")
    print(f"📄 Config: {yaml_path}")
    print(f"\n📊 Toplam:")
    print(f"   Train: {total_counts['train']} görüntü")
    print(f"   Val:   {total_counts['val']} görüntü")
    print(f"   Test:  {total_counts['test']} görüntü")
    print(f"\n🏷️ Sınıflar: {list(CLASS_NAMES.values())}")
    print("\n🚀 Model eğitmek için:")
    print(f"   yolo detect train data={yaml_path} model=yolo11n.pt epochs=100 imgsz=640")
