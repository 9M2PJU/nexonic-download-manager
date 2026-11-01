# Layout of the Mac installer window (read by dmgbuild from scripts/build-app.sh).
import os.path

app = defines["app"]
files = [app, defines["extension"], defines["xpi"]]
symlinks = {"Applications": "/Applications"}
icon = defines["icns"]
background = defines["background"]
format = "UDZO"
window_rect = ((200, 140), (660, 500))
default_view = "icon-view"
show_status_bar = False
show_tab_view = False
show_toolbar = False
show_pathbar = False
show_sidebar = False
icon_size = 96

# Terms of use shown when the disk image is opened; the user must press Agree.
with open(defines.get("terms", "packaging/TERMS.txt"), encoding="utf-8") as f:
    _terms = f.read()
license = {
    "default-language": "en_US",
    "licenses": {"en_US": _terms},
    "buttons": {
        "en_US": (
            "English",
            "Agree",
            "Disagree",
            "Print",
            "Save",
            "To install Nexonic Download Manager, please read and agree to the Terms of Use and License Agreement. "
            "Click Disagree if you do not agree.",
        )
    },
}
text_size = 13
icon_locations = {
    os.path.basename(app): (180, 165),
    "Applications": (480, 165),
    os.path.basename(defines["extension"]): (250, 360),
    os.path.basename(defines["xpi"]): (410, 360),
}
