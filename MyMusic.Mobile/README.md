# Development

## Prerequisites

### 1. Install Android Studio (Flatpak)

```bash
flatpak install com.google.AndroidStudio
flatpak run com.google.AndroidStudio
```

### 2. Install ADB (Android Debug Bridge)

```bash
sudo apt update
sudo apt install adb openjdk-21-jdk
```

### 3. Configure Gradle Version

Edit `android/gradle/wrapper/gradle-wrapper.properties`:

```properties
distributionUrl=https\://services.gradle.org/distributions/gradle-8.13-bin.zip
# Note: You can also use the latest 8.x version (e.g., gradle-8.x-bin.zip)
```

### 4. Configure Android SDK Path

Create `android/local.properties`:

```properties
sdk.dir=/home/pedro/Android/Sdk
```

### 5. Configure Node.js Path (if using devbox/nix)

If using devbox/nix, edit `android/gradle.properties` and add:

```properties
org.gradle.nodejs=/home/pedro/Projects/MediaCenter/my-music/.devbox/nix/profile/default/bin/node
```

### 6. Run the App

```bash
cd MyMusic.Mobile
npx expo run:android
```

## Notes

- First run may take several minutes to download Gradle dependencies
- Ensure the Android emulator or device is running before executing
- Use `npx expo start` for development server only (Metro bundler)
- If using devbox/nix, stop the Gradle daemon before building to ensure it uses the current PATH:
  ```bash
  cd android && ./gradlew --stop
  cd .. && npx expo run:android
  ```