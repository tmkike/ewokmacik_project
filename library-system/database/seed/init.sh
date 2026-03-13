#!/bin/bash

echo "Importing books..."

mongoimport \
--db library \
--collection books \
--file /docker-entrypoint-initdb.d/books.json \
--jsonArray

echo "Books imported."