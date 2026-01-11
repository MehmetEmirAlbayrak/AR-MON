import os
import json
from datetime import datetime

import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torchvision import datasets, transforms, models

# =============== Ayarlar ===============
data_root = "data"  # az önce oluşturduğun klasör
batch_size = 32
num_epochs = 10   # istersen daha az / fazla yapabilirsin
lr = 1e-4
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# =============== Transformlar ===============
train_transforms = transforms.Compose([
    transforms.Resize((256, 256)),
    transforms.RandomResizedCrop(224),
    transforms.RandomHorizontalFlip(),
    transforms.ColorJitter(brightness=0.3, contrast=0.3, saturation=0.3),
    transforms.ToTensor(),
])

val_transforms = transforms.Compose([
    transforms.Resize((256, 256)),
    transforms.CenterCrop(224),
    transforms.ToTensor(),
])

train_dir = os.path.join(data_root, "train")
val_dir = os.path.join(data_root, "val")

train_dataset = datasets.ImageFolder(train_dir, transform=train_transforms)
val_dataset = datasets.ImageFolder(val_dir, transform=val_transforms)

train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True, num_workers=2)
val_loader = DataLoader(val_dataset, batch_size=batch_size, shuffle=False, num_workers=2)

class_names = train_dataset.classes
num_classes = len(class_names)
print("Classes:", class_names)

# =============== Model ===============
# Yeni torchvision sürümü:
try:
    weights = models.ResNet18_Weights.DEFAULT
    model = models.resnet18(weights=weights)
except AttributeError:
    # Eski sürüm:
    model = models.resnet18(pretrained=True)

# Son katmanı sınıf sayına göre değiştir
in_features = model.fc.in_features
model.fc = nn.Linear(in_features, num_classes)

model = model.to(device)

criterion = nn.CrossEntropyLoss()
optimizer = torch.optim.Adam(model.parameters(), lr=lr)

# =============== Train-loop ===============
def train_one_epoch(epoch):
    model.train()
    total_loss = 0.0
    correct = 0
    total = 0

    for images, labels in train_loader:
        images = images.to(device)
        labels = labels.to(device)

        optimizer.zero_grad()
        outputs = model(images)
        loss = criterion(outputs, labels)
        loss.backward()
        optimizer.step()

        total_loss += loss.item() * images.size(0)
        _, preds = torch.max(outputs, 1)
        correct += (preds == labels).sum().item()
        total += labels.size(0)

    epoch_loss = total_loss / total
    epoch_acc = correct / total
    print(f"Epoch {epoch+1} - Train Loss: {epoch_loss:.4f}, Acc: {epoch_acc:.4f}")
    return epoch_loss, epoch_acc


def eval_one_epoch(epoch):
    model.eval()
    total_loss = 0.0
    correct = 0
    total = 0

    with torch.no_grad():
        for images, labels in val_loader:
            images = images.to(device)
            labels = labels.to(device)

            outputs = model(images)
            loss = criterion(outputs, labels)

            total_loss += loss.item() * images.size(0)
            _, preds = torch.max(outputs, 1)
            correct += (preds == labels).sum().item()
            total += labels.size(0)

    epoch_loss = total_loss / total
    epoch_acc = correct / total
    print(f"Epoch {epoch+1} - Val   Loss: {epoch_loss:.4f}, Acc: {epoch_acc:.4f}")
    return epoch_loss, epoch_acc


best_val_acc = 0.0
save_dir = "checkpoints"
os.makedirs(save_dir, exist_ok=True)

for epoch in range(num_epochs):
    train_one_epoch(epoch)
    _, val_acc = eval_one_epoch(epoch)

    # En iyi modeli kaydet
    if val_acc > best_val_acc:
        best_val_acc = val_acc
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        model_path = os.path.join(save_dir, f"biome_resnet18_best_{timestamp}.pth")
        torch.save(model.state_dict(), model_path)
        print(f"New best model saved at {model_path} (val_acc={val_acc:.4f})")

# Class index mapping kaydet
mapping_path = os.path.join(save_dir, "class_indices.json")
with open(mapping_path, "w") as f:
    json.dump({i: cls for i, cls in enumerate(class_names)}, f, indent=2)
print("Class mapping saved to", mapping_path)
print("Best val acc:", best_val_acc)
