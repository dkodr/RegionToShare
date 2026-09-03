1.12
- Fix #49: Optionally show the desktop wallpaper instead of the black background in the parts of the region that no other window covers ("Show desktop wallpaper").
- Window anchor: a 3x3 grid pins the window to an edge or corner of the screen; the anchor is kept when the size changes and released when the window is dragged.
- Aspect ratio lock (16:9, 3:2, 4:3), applied while resizing and when a resolution is entered; window edges snap to the screen edges while resizing.
- Fix #85: the dotted background pattern is gone; the background is a solid color chosen from a list of pastel colors (default black). The theme color is chosen from a list of five vivid colors.
- Fix #45: the main window is sent back behind the region again when a meeting app changes the window order.
- More predefined resolutions (1280x720, 1600x900, 2560x1440, 3200x1800, 3840x2160), merged into an existing resolutions.txt.

1.11
- Fix #80: Support native ARM64, contributed by Stefan Forstenlechner <stefan@forstenlechner.dev>
- Fix #79: Do not default to start activated
- Fix #72: automatically recover from corrupt settings file.

1.10
- Fix #60: Issues when restoring the window position on the secondary screen with different DPI settings

1.9
- Fix #69: restore windows position is not always correct, since glass frame thickness is not constant.
 
1.8
- Fix: Resolution not updated after restore from minimized state.

1.7
- Fix #62: adjust window by the glass frame size to work better with FancyZones

1.6
- Increase version to fix broken appstore upload

1.5
- Allow to start in activated mode

1.4
- Fix #44: Theme color is configurable, backgound image is less distracting
- Fix #46: Close button is now partially visible when maximized
- Fix #48: Optionally draw a shadow cursor
- FPS is now configurable
- Fix: Separation layer sometimes not visible

1.3
- Fix #41: Region of secondary monitor not shareable

1.2
- Fix #34: Incorrect pixel resolution 
- Fix #33: Window size can be selected from list
- Fix #21: Add app version in the title bar

1.1
- Fix #5: Flickering on Win11
- Fix #18: Double mouse cursors
- Fix #11: Remember size and position

1.0
- Initial version