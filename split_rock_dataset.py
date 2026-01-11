import os
import shutil
import random

# rock_yolo içinde valid ve test klasörleri oluştur
BASE_DIR = r"C:\Users\Mehmet Emir\Ar-Backend\rock_yolo"

train_images = os.path.join(BASE_DIR, "train", "images")
train_labels = os.path.join(BASE_DIR, "train", "labels")

# Hedef klasörler
for split in ["valid", "test"]:
    os.makedirs(os.path.join(BASE_DIR, split, "images"), exist_ok=True)
    os.makedirs(os.path.join(BASE_DIR, split, "labels"), exist_ok=True)

# Tüm train görüntülerini al
all_images = [f for f in os.listdir(train_images) if f.endswith(('.jpg', '.jpeg', '.png'))]
random.seed(42)
random.shuffle(all_images)

total = len(all_images)
# %15 valid, %10 test, %75 train olarak ayır
val_count = int(total * 0.15)
test_count = int(total * 0.10)

val_images = all_images[:val_count]
test_images = all_images[val_count:val_count + test_count]

print(f"Toplam: {total} görüntü")
print(f"Valid'e taşınacak: {len(val_images)}")
print(f"Test'e taşınacak: {len(test_images)}")
print(f"Train'de kalacak: {total - len(val_images) - len(test_images)}")

def move_files(image_list, target_split):
    for img_name in image_list:
        # Image taşı
        src_img = os.path.join(train_images, img_name)
        dst_img = os.path.join(BASE_DIR, target_split, "images", img_name)
        shutil.move(src_img, dst_img)
        
        # Label taşı
        label_name = os.path.splitext(img_name)[0] + ".txt"
        src_lbl = os.path.join(train_labels, label_name)
        dst_lbl = os.path.join(BASE_DIR, target_split, "labels", label_name)
        if os.path.exists(src_lbl):
            shutil.move(src_lbl, dst_lbl)

move_files(val_images, "valid")
move_files(test_images, "test")

print("\nBitti! rock_yolo şimdi valid ve test split'lerine sahip.")

