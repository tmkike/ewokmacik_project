# Library System Database

This repository contains the MongoDB database setup for the Library System project.

The database runs in a Docker container and automatically loads example book data.

## Requirements

Before running the database you need:

- Docker Desktop installed
- Docker Compose

## Start the database

Open a terminal in the `library-system` folder and run:

```bash
docker compose up -d
```

This will start the MongoDB container.

## Stop the database

```bash
docker compose down
```

## Database information

Database name:

```
library
```

Collection:

```
books
```

The database automatically loads **20 example books** when the container starts.

## MongoDB connection

MongoDB runs on:

```
localhost:27017
```

This can be used by the backend service.

## REST API

The project also contains a Node.js/Express backend in the `backend` folder.

Run the API and MongoDB together:

```bash
docker compose up --build
```

The API runs on:

```text
http://localhost:3000
```

Available endpoints:

```text
GET    /api/books
GET    /api/books/:id
POST   /api/books
PUT    /api/books/:id
PATCH  /api/books/:id/availability
DELETE /api/books/:id
```

Search and filter examples:

```text
GET /api/books?search=tolkien
GET /api/books?genre=Fantasy
GET /api/books?available=true
GET /api/books?author=orwell
```

Example book payload:

```json
{
  "title": "Dune",
  "author": "Frank Herbert",
  "year": 1965,
  "genre": "Science Fiction",
  "available": true
}
```

## Project structure

```
library-system
│
├ docker-compose.yml
│
└ database
  └ seed
     ├ books.json
     └ init.sh
`

``## Kubernetes deployment

Create namespace:

```bash
kubectl apply -f k8s/namespace.yaml
```

Deploy MongoDB:

```bash
kubectl apply -f k8s/mongodb-deployment.yaml
```

Create service:

```bash
kubectl apply -f k8s/mongodb-service.yaml
```

Check pods:

```bash
kubectl get pods
```
