clang++ -fPIC -shared code.cpp -g -o nativelib.dylib -arch i386 -arch x86_64
cp nativelib.dylib ../src/NativeLib/runtimes/osx.10.11-x64/native/
cp nativelib.dylib ../src/NativeLib/runtimes/osx.10.10-x64/native/
cp nativelib.dylib ../src/NativeLib/runtimes/osx.10.9-x64/native/
