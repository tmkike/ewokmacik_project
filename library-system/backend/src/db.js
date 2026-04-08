const { MongoClient } = require('mongodb');

const uri = process.env.MONGODB_URI || 'mongodb://localhost:27017';
const dbName = process.env.MONGODB_DB || 'library';

let client;
let db;

async function connectToDatabase() {
  if (db) {
    return db;
  }

  try {
    // Az alkalmazás teljes élettartama alatt ugyanazt a klienskapcsolatot használjuk.
    if (!client) {
      client = new MongoClient(uri);
    }

    await client.connect();
    db = client.db(dbName);

    return db;
  } catch (error) {
    client = undefined;
    db = undefined;
    throw error;
  }
}

async function closeDatabaseConnection() {
  if (!client) {
    return;
  }

  await client.close();
  client = undefined;
  db = undefined;
}

module.exports = {
  connectToDatabase,
  closeDatabaseConnection,
};
