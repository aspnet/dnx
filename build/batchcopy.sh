#!/usr/bin/env bash

echo "Copy From: $1"
echo "       To: $2"

src=${1//\\//}
dst=${2//\\//}

if [ ! -d "$dst" ]; then
  mkdir -p $dst
fi

cp $src $dst
rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi