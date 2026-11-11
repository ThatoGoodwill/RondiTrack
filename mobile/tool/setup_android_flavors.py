#!/usr/bin/env python3
"""Configure three Android product flavors after flutter create (AGP 9 compatible)."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
gradle = root / "android/app/build.gradle.kts"
if not gradle.exists():
    raise SystemExit("Run flutter create . --platforms android,ios first")
text = gradle.read_text(encoding="utf-8")
if 'create("dev")' not in text:
    marker = "    buildTypes {"
    if marker not in text:
        raise SystemExit("Unexpected Gradle template; configure flavors manually")
    flavor = """    flavorDimensions += "environment"
    productFlavors {
        create("dev") { dimension = "environment"; applicationIdSuffix = ".dev" }
        create("staging") { dimension = "environment"; applicationIdSuffix = ".staging" }
        create("prod") { dimension = "environment" }
    }
"""
    text = text.replace(marker, flavor + marker, 1)
    gradle.write_text(text, encoding="utf-8")
manifest = root / "android/app/src/main/AndroidManifest.xml"
if manifest.exists():
    content = manifest.read_text(encoding="utf-8")
    content = content.replace('android:label="ronditrack_mobile"', 'android:label="@string/app_name"')
    manifest.write_text(content, encoding="utf-8")
for name, title in [("dev", "RondiTrack Dev"), ("staging", "RondiTrack Staging"), ("prod", "RondiTrack")]:
    folder = root / "android/app/src" / name / "res/values"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "strings.xml").write_text('<resources><string name="app_name">' + title + '</string></resources>\n', encoding="utf-8")
print("Android flavours configured: dev, staging, prod.")
