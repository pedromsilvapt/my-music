cmake_minimum_required(VERSION 3.13)

project(mymusic-xxhash CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(mymusic-xxhash SHARED src/main/cpp/xxhash_jni.cpp)

# xxhash.h is tuned for -O3; keep it even in debug builds, where hashing would otherwise crawl
target_compile_options(mymusic-xxhash PRIVATE -O3 -fno-exceptions -fno-rtti)
