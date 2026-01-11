import os
import glob
import shutil

# --- AYARLAR --- #
BASE_DIR = r"C:\Users\Mehmet Emir\Ar-Backend"   # hepsinin olduğu üst klasör

# kaynaktaki klasör adları ve yeni class id'leri
DATASETS = [
    ("grass_yolo", 0),  # (folder_name, new_class_id)
    ("tree_yolo",  1),
    ("rock_yolo",  2),
]

TARGET_NAME = "biome_merged"      # yeni dataset klasörü
SPLITS = ["train", "val", "test"]  # hedef split'ler
# Roboflow "valid" kullanıyor, biz "val" olarak kaydediyoruz
SPLIT_MAP = {"train": "train", "val": "valid", "test": "test"}
# -------------- #

target_root = os.path.join(BASE_DIR, TARGET_NAME)

for split in SPLITS:
    os.makedirs(os.path.join(target_root, split, "images"), exist_ok=True)
    os.makedirs(os.path.join(target_root, split, "labels"), exist_ok=True)

def rewrite_labels_and_copy(
    src_root: str, dst_root: str, split: str, class_id: int, prefix: str
):
    # Roboflow'daki kaynak klasör adı (valid vs val)
    src_split = SPLIT_MAP.get(split, split)
    src_img_dir = os.path.join(src_root, src_split, "images")
    src_lbl_dir = os.path.join(src_root, src_split, "labels")
    
    # Kaynak klasör yoksa atla
    if not os.path.exists(src_img_dir):
        print(f"  [SKIP] {prefix}/{src_split} bulunamadı")
        return

    dst_img_dir = os.path.join(dst_root, split, "images")
    dst_lbl_dir = os.path.join(dst_root, split, "labels")

    img_paths = glob.glob(os.path.join(src_img_dir, "*.*"))

    for img_path in img_paths:
        base = os.path.basename(img_path)
        name, ext = os.path.splitext(base)

        # dosya adı çakışmasın diye prefix ekle
        new_name = f"{prefix}_{name}{ext}"
        new_img_path = os.path.join(dst_img_dir, new_name)

        # image kopyala
        shutil.copy2(img_path, new_img_path)

        # label dosyası
        src_label_path = os.path.join(src_lbl_dir, f"{name}.txt")
        dst_label_path = os.path.join(dst_lbl_dir, f"{prefix}_{name}.txt")

        if not os.path.exists(src_label_path):
            # bazen boş frame olabilir – atla
            continue

        with open(src_label_path, "r") as f_in, open(dst_label_path, "w") as f_out:
            for line in f_in:
                parts = line.strip().split()
                if len(parts) < 5:
                    continue
                # ilk eleman class id – bunu değiştir
                parts[0] = str(class_id)
                f_out.write(" ".join(parts) + "\n")


if __name__ == "__main__":
    for folder_name, new_cls in DATASETS:
        src_root = os.path.join(BASE_DIR, folder_name)
        prefix = folder_name.split("_")[0]  # "grass_yolo" -> "grass"
        print(f"\n[{folder_name}] işleniyor (class_id={new_cls})...")

        for split in SPLITS:
            rewrite_labels_and_copy(
                src_root, target_root, split, new_cls, prefix
            )

    # data.yaml oluştur
    yaml_path = os.path.join(target_root, "data.yaml")
    target_path_unix = target_root.replace("\\", "/")
    yaml_text = f"""# biome merged dataset
path: {target_path_unix}

train: train/images
val: val/images
test: test/images

names:
  0: grass
  1: tree
  2: rock
"""
    with open(yaml_path, "w", encoding="utf-8") as f:
        f.write(yaml_text)

    print("Bitti! Yeni dataset:", target_root)
    print("data.yaml:", yaml_path)
