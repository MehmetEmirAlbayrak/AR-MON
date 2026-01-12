import os
import glob
import shutil
import random

# Dataset'i train/val/test'e böl
BASE_DIR = r"C:\Users\Mehmet Emir\Ar-Backend"
NATURE_DIR = os.path.join(BASE_DIR, "nature_yolo")

# Split oranları
TRAIN_RATIO = 0.70  # 70% train
VAL_RATIO = 0.20     # 20% validation
TEST_RATIO = 0.10    # 10% test

# Klasörleri oluştur
for split in ["train", "val", "test"]:
    os.makedirs(os.path.join(NATURE_DIR, split, "images"), exist_ok=True)
    os.makedirs(os.path.join(NATURE_DIR, split, "labels"), exist_ok=True)

# Mevcut train klasöründen fotoğrafları al
# Önce train klasörünü temp'e kopyala
temp_train_dir = os.path.join(NATURE_DIR, "_temp_train")
if os.path.exists(temp_train_dir):
    shutil.rmtree(temp_train_dir)
shutil.copytree(os.path.join(NATURE_DIR, "train"), temp_train_dir)

train_img_dir = os.path.join(temp_train_dir, "images")
train_lbl_dir = os.path.join(temp_train_dir, "labels")

img_paths = glob.glob(os.path.join(train_img_dir, "*.jpg"))
random.shuffle(img_paths)

total = len(img_paths)
train_end = int(total * TRAIN_RATIO)
val_end = train_end + int(total * VAL_RATIO)

splits = {
    "train": img_paths[:train_end],
    "val": img_paths[train_end:val_end],
    "test": img_paths[val_end:]
}

# Dosyaları kopyala
for split_name, paths in splits.items():
    print(f"\n[{split_name}] Processing {len(paths)} images...")
    
    for img_path in paths:
        base = os.path.basename(img_path)
        name, ext = os.path.splitext(base)
        
        # Image kopyala
        dst_img = os.path.join(NATURE_DIR, split_name, "images", base)
        shutil.copy2(img_path, dst_img)
        
        # Label kopyala
        src_label = os.path.join(train_lbl_dir, f"{name}.txt")
        if os.path.exists(src_label):
            dst_label = os.path.join(NATURE_DIR, split_name, "labels", f"{name}.txt")
            shutil.copy2(src_label, dst_label)

# data.yaml oluştur
yaml_content = f"""# Nature YOLO Dataset - Custom Photos
path: {NATURE_DIR.replace(chr(92), '/')}

train: train/images
val: val/images
test: test/images

nc: 6
names:
  0: bush
  1: grass
  2: leaf
  3: rock
  4: stone
  5: tree
"""

yaml_path = os.path.join(NATURE_DIR, "data.yaml")
with open(yaml_path, "w", encoding="utf-8") as f:
    f.write(yaml_content)

# Temp klasörü sil
if os.path.exists(temp_train_dir):
    shutil.rmtree(temp_train_dir)

print("\n" + "="*50)
print("✅ Dataset hazırlandı!")
print("="*50)
print(f"\n📊 Split:")
print(f"   Train: {len(splits['train'])} images")
print(f"   Val:   {len(splits['val'])} images")
print(f"   Test:  {len(splits['test'])} images")
print(f"\n📄 Config: {yaml_path}")
print("\n🚀 Eğitim komutu:")
print(f"   yolo detect train data={yaml_path} model=yolov8n.pt epochs=100 imgsz=640")
