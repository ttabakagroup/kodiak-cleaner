# Builds KodiakCleanerShell.dll, the Windows 11 menu command for the Microsoft Store package (see KodiakCleanerShell.cpp).
# packaging/store/build-msix.ps1 runs: cmake -S source/shell -B <dir> -A x64 && cmake --build <dir> --config Release
cmake_minimum_required(VERSION 3.20)
project(KodiakCleanerShell CXX)
add_library(KodiakCleanerShell SHARED KodiakCleanerShell.cpp KodiakCleanerShell.def)
target_compile_definitions(KodiakCleanerShell PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_features(KodiakCleanerShell PRIVATE cxx_std_17)
if(MSVC)
  target_compile_options(KodiakCleanerShell PRIVATE /W4 /WX /guard:cf)
  target_link_options(KodiakCleanerShell PRIVATE /GUARD:CF /DYNAMICBASE /NXCOMPAT)
  # Static CRT, so the package needs no Visual C++ runtime.
  set_property(TARGET KodiakCleanerShell PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded")
endif()
target_link_libraries(KodiakCleanerShell PRIVATE shell32 shlwapi ole32 uuid)
