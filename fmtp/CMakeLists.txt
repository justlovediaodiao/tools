cmake_minimum_required(VERSION 3.20)

project(fmtp LANGUAGES CXX)

if(NOT WIN32)
    message(FATAL_ERROR "fmtp requires Windows and the Windows Portable Devices SDK headers.")
endif()

add_executable(fmtp
    src/main.cpp
    src/mtp.cpp
    src/reporter.cpp
    src/sync.cpp
    src/text.cpp
)
target_include_directories(fmtp PRIVATE include)
target_compile_features(fmtp PRIVATE cxx_std_17)

if(MSVC)
    target_compile_options(fmtp PRIVATE /W4 /permissive- /utf-8)
endif()

target_link_libraries(fmtp PRIVATE
    Ole32
    OleAut32
    PortableDeviceGUIDs
)
