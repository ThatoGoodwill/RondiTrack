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

# Week 11 networking: INTERNET is required by release builds as well.
if manifest.exists():
    content = manifest.read_text(encoding="utf-8")
    permission = '<uses-permission android:name="android.permission.INTERNET" />'
    if permission not in content:
        content = content.replace("<application", permission + "\n    <application", 1)
    manifest.write_text(content, encoding="utf-8")

# Only dev permits plain HTTP, and only to the emulator host from dev.json.
dev = root / "android/app/src/dev"
dev.mkdir(parents=True, exist_ok=True)
(dev / "AndroidManifest.xml").write_text("""<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <application android:networkSecurityConfig="@xml/dev_network_security_config" />
</manifest>
""", encoding="utf-8")
dev_xml = dev / "res/xml"
dev_xml.mkdir(parents=True, exist_ok=True)
(dev_xml / "dev_network_security_config.xml").write_text("""<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
  <base-config cleartextTrafficPermitted="false" />
  <domain-config cleartextTrafficPermitted="true">
    <domain includeSubdomains="false">10.0.2.2</domain>
  </domain-config>
</network-security-config>
""", encoding="utf-8")
