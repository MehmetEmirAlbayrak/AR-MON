# AR-MON

An AR-based mobile game project similar to Pokemon. Offers an experience of catching, battling, and collecting Pokemon in real-world environments.

## 🎮 Features

- **AR Integration**: View and interact with Pokemon in real-world environments
- **Pokemon Catching**: Catch wild Pokemon using Pokeball mechanics
- **Battle System**: Pokemon battles and battle management
- **Collection**: Pokemon inventory and detailed information display
- **Biome System**: Different Pokemon spawns in different regions
- **Frame Analysis**: AI-based image analysis and object detection
- **Server Integration**: Communication with backend server

## 🛠️ Technologies

- **Unity Engine**: 6000.2.8f or higher
- **AR Foundation**: For AR features
- **TextMesh Pro**: For UI text
- **Input System**: Advanced input management

## 📋 Requirements

- Unity 2021.3 or higher
- Required SDKs for Android/iOS development
- AR-capable device

## 🚀 Installation

1. Clone the project:
```bash
git clone https://github.com/MehmetEmirAlbayrak/AR-MON.git
cd AR-MON
```

2. Open the project in Unity Hub

3. Make sure the required packages are installed in Unity Editor:
   - AR Foundation
   - TextMesh Pro
   - Input System

4. Update the IP address in `Assets/ServerConfig.asset` file with your own server IP for server configuration

## 📁 Project Structure

```
Assets/
├── Scripts/              # C# script files
│   ├── GameManager.cs
│   ├── BattleManager.cs
│   ├── PokemonData.cs
│   ├── FrameAnalyzer.cs
│   └── ...
├── Scenes/               # Unity scene files
├── Prefabs/              # Prefab objects
├── XR/                   # AR/XR settings
└── ServerConfig.asset    # Server configuration file
```

## ⚙️ Configuration

### Server Settings

You can edit the server IP address by opening the `Assets/ServerConfig.asset` file in Unity Editor. By default, `http://192.168.1.102:5001` is used.

## 🎯 Usage

1. Open the project in Unity Editor
2. Run the main scene
3. Grant AR camera permissions
4. Use Pokeball to catch Pokemon
5. View your Pokemon from the inventory

## 📝 Notes

- The project uses AR Foundation
- Backend service must be running for server integration
- AI model service is required for frame analysis feature

## 👤 Developer

**Mehmet Emir Albayrak**

## 📄 License

This is a private project.
