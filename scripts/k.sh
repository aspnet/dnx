#!/bin/bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

#TODO: remove KRE_ var env
if [ -z "$DOTNET_APPBASE" ]; then
    DOTNET_APPBASE="$KRE_APPBASE"
fi

if [ -z "$DOTNET_APPBASE" ]; then
    DOTNET_APPBASE=`pwd`
fi

if [ -f "$DIR/k-$1" ]; then
    exec $DIR/k-$1 "$@"
else
    exec $DIR/dotnet --appbase "$DOTNET_APPBASE" Microsoft.Framework.ApplicationHost "$@"  
fi
