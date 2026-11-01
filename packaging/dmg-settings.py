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
text_size = 13
icon_locations = {
    os.path.basename(app): (180, 165),
    "Applications": (480, 165),
    os.path.basename(defines["extension"]): (250, 360),
    os.path.basename(defines["xpi"]): (410, 360),
}
