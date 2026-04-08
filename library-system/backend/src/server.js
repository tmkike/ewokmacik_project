require('dotenv').config();

const cors = require('cors');
const express = require('express');

const booksRouter = require('./books.routes');
const { closeDatabaseConnection, connectToDatabase } = require('./db');

const app = express();
const port = process.env.PORT || 3000;

let server;
let shuttingDown = false;

app.use(cors());
app.use(express.json());

app.get('/health', (_req, res) => {
  res.json({ status: 'ok' });
});

app.use('/api/books', booksRouter);

app.use((err, _req, res, _next) => {
  console.error('Váratlan backend hiba:', err);

  if (res.headersSent) {
    return;
  }

  res.status(500).json({ message: 'Belső szerverhiba történt.' });
});

async function shutdown(signal) {
  if (shuttingDown) {
    return;
  }

  shuttingDown = true;
  console.log(`${signal} jel érkezett, a szerver leáll...`);

  try {
    if (server) {
      await new Promise((resolve, reject) => {
        server.close((error) => {
          if (error) {
            reject(error);
            return;
          }

          resolve();
        });
      });
    }

    await closeDatabaseConnection();
    process.exit(0);
  } catch (error) {
    console.error('Hiba történt a leállás közben:', error);
    process.exit(1);
  }
}

async function startServer() {
  await connectToDatabase();

  server = app.listen(port, () => {
    console.log(`A könyvtári API a ${port}-es porton fut.`);
  });
}

process.on('SIGINT', () => {
  void shutdown('SIGINT');
});

process.on('SIGTERM', () => {
  void shutdown('SIGTERM');
});

startServer().catch((error) => {
  console.error('A szerver indítása nem sikerült:', error);
  process.exit(1);
});
