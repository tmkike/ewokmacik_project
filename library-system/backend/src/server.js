require('dotenv').config();

const express = require('express');
const cors = require('cors');
const booksRouter = require('./books.routes');
const { connectToDatabase, closeDatabaseConnection } = require('./db');

const app = express();
const port = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

app.get('/health', (_req, res) => {
  res.json({ status: 'ok' });
});

app.use('/api/books', booksRouter);

app.use((err, _req, res, _next) => {
  console.error(err);
  res.status(500).json({ message: 'Internal server error' });
});

async function startServer() {
  await connectToDatabase();

  app.listen(port, () => {
    console.log(`Library API is running on port ${port}`);
  });
}

process.on('SIGINT', async () => {
  await closeDatabaseConnection();
  process.exit(0);
});

process.on('SIGTERM', async () => {
  await closeDatabaseConnection();
  process.exit(0);
});

startServer().catch((error) => {
  console.error('Failed to start server', error);
  process.exit(1);
});
