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
```