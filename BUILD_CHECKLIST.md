# Android build checklist

1. Confirm Unity `2022.3.62f1` is used by the workflow.
2. Confirm `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` are configured as repository Actions secrets.
3. Run **Actions → Build JungleRide Android APK → Run workflow**.
4. Wait for the `Unity Android APK` job to finish successfully.
5. Download the `JungleRide-APK-<run number>` artifact.
6. Install the APK on an ARM64 Android device running Android 7.0/API 24 or later.
7. Test startup, camera follow, touch controls, keyboard fallback, braking, reverse roll, tree/log collision, and device rotation behavior.
8. Record startup time and FPS on the lowest supported device.

The current workflow produces an unsigned/debug APK suitable for direct installation. Do not distribute it through an app store until a release keystore, signing configuration, privacy policy, icon, and store metadata are added.
