# ARCHive UI Polish Status

The dedicated interface pass is implemented in `1.0.0-rc2`.

Completed:

- reduced the window from 940 x 720 to a fixed 680 x 500;
- replaced the generic Windows title bar with matching graphite chrome;
- removed the page scrollbar and the large selected-source path list;
- replaced the source list with a count summary and compact Clear action;
- made the active progress/result state replace the idle Start panel;
- branded the installer with the same dark palette and ARCHive icon;
- reduced global button, field, panel-padding, and section-spacing dimensions;
- retained readable Segoe UI text and native keyboard/accessibility semantics;
- replaced the large light panels with a graphite-and-amber native WPF theme;
- used the supplied application icon;
- kept the multi-megabyte reference backgrounds out of the runtime package;
- translated the supplied loading-bar concept into an accurate native progress
  bar without random or invented percentage movement;
- reduced **Open Diagnostic Log** to a compact secondary action;
- preserved readable progress and result details inside the fixed frame.

Release-candidate acceptance still requires manual checks at 100%, 125%, and
150% scaling on the Windows 11 VM and physical PC.
