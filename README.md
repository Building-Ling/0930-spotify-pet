# 0930 Spotify Pet

Windows 桌宠。Spotify 播放时它会唱歌，暂停时会睡觉。右键角色可以切换钢琴模式、窗口置顶或关闭应用。播放器支持播放、暂停、切歌和拖动进度条。

0930 is a Windows desktop pet that sings while Spotify plays and sleeps when playback pauses. Right-click the pet to toggle **Piano Mode** (「钢琴模式」), keep the window on top, or close it. The player supports play/pause, track skipping, and seeking.

<img src="SpotifyPet/Assets/0930-piano.png" alt="0930 desktop pet playing a compact piano" width="240">

钢琴模式效果图 · Piano Mode artwork

## 本地开发 / Run locally

在 Windows 上安装 .NET 10 SDK 和 Spotify 桌面版。克隆仓库后，在仓库根目录运行：

On Windows, install the .NET 10 SDK and the Spotify desktop app. Clone this repository and run the following command from its root.

```powershell
dotnet run --project SpotifyPet/SpotifyPet.csproj
```

登录 Spotify 并播放一首歌，即可测试桌宠与播放器的联动。

Sign in to Spotify and play a track to test the controls and animations.

## 构建安装包 / Build the installer

在 Windows 上用 .NET 10 SDK 发布，然后用 Inno Setup 6.7 或更新版本生成安装包。将下面的 `ISCC.exe` 替换成你本机编译器的路径。

Publish on Windows with the .NET 10 SDK, then build the installer with Inno Setup 6.7 or later. Replace `ISCC.exe` with the path to your local compiler.

```powershell
dotnet publish SpotifyPet/SpotifyPet.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
& 'ISCC.exe' "/DPayloadDir=$PWD\dist\win-x64" "/O$PWD\dist" SpotifyPet/Installer.iss
```

生成的安装包没有代码签名。启用了 Smart App Control 的 Windows 设备可能拦截桌宠启动。

This build produces an unsigned installer and app. Windows Smart App Control may block the app from launching.

## 声明 / Disclaimer

本项目为个人学习和非商业用途制作，与 Spotify 及角色权利人无关联。角色形象的相关权利归其权利人所有。仓库公开不授予角色素材的再使用权。

This project was created for personal learning and noncommercial use. It is unaffiliated with Spotify and the character rights holder. The character rights belong to their respective owners. Publishing this repository does not grant permission to reuse the character artwork.
