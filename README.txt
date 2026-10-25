JUNGLE RIDE - Unity 2022.3 project (v1)

What it is
- Offline cycling game: a rider on a bicycle on a winding mud road through a jungle.
- Big hollow tree trunks lie across the road - ride through them.
- Trees, giant trees with vines, bushes, ferns, grass, rocks, puddles, fog.
- Controls: < and > to steer, PEDAL to ride, BRAKE to stop (hold BRAKE when stopped to roll back).

How it is built
- Everything (jungle, bike, rider) is created by scripts when the game starts.
- Assets/Editor/JungleBuild.cs makes the scene and the APK during the GitHub build.
- Build with the "Unity ZIP to APK" GitHub workflow (unity-zip-to-apk.yml).

App name / package (change in Assets/Editor/JungleBuild.cs if you want):
- Jungle Ride / com.appforge.jungleride
