#!/bin/bash

echo "Importing books into the book-service database..."

mongoimport \
  --db books \
  --collection books \
  --file /docker-entrypoint-initdb.d/books.json \
  --jsonArray

echo "Books imported."
