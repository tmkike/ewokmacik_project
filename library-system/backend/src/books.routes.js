const express = require('express');
const { ObjectId } = require('mongodb');
const { connectToDatabase } = require('./db');
const { validateBookPayload } = require('./book-validation');

const router = express.Router();

function getBooksCollection(db) {
  return db.collection('books');
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function buildBooksFilter(query) {
  const filter = {};

  if (query.search) {
    const searchRegex = new RegExp(escapeRegExp(query.search), 'i');
    filter.$or = [
      { title: searchRegex },
      { author: searchRegex },
      { genre: searchRegex },
    ];
  }

  if (query.genre) {
    filter.genre = new RegExp(`^${escapeRegExp(query.genre)}$`, 'i');
  }

  if (query.available !== undefined) {
    if (query.available !== 'true' && query.available !== 'false') {
      return {
        error: 'available query parameter must be true or false',
      };
    }

    filter.available = query.available === 'true';
  }

  if (query.author) {
    filter.author = new RegExp(escapeRegExp(query.author), 'i');
  }

  return { filter };
}

function parseBookId(id) {
  return ObjectId.isValid(id) ? new ObjectId(id) : undefined;
}

router.get('/', async (req, res, next) => {
  try {
    const { filter, error } = buildBooksFilter(req.query);

    if (error) {
      return res.status(400).json({ message: error });
    }

    const db = await connectToDatabase();
    const books = await getBooksCollection(db)
      .find(filter)
      .sort({ title: 1 })
      .toArray();

    return res.json(books);
  } catch (error) {
    return next(error);
  }
});

router.get('/:id', async (req, res, next) => {
  try {
    const id = parseBookId(req.params.id);

    if (!id) {
      return res.status(400).json({ message: 'Invalid book id' });
    }

    const db = await connectToDatabase();
    const book = await getBooksCollection(db).findOne({ _id: id });

    if (!book) {
      return res.status(404).json({ message: 'Book not found' });
    }

    return res.json(book);
  } catch (error) {
    return next(error);
  }
});

router.post('/', async (req, res, next) => {
  try {
    const { book, errors } = validateBookPayload(req.body);

    if (errors.length > 0) {
      return res.status(400).json({ message: 'Invalid book data', errors });
    }

    const db = await connectToDatabase();
    const result = await getBooksCollection(db).insertOne(book);

    return res.status(201).json({
      _id: result.insertedId,
      ...book,
    });
  } catch (error) {
    return next(error);
  }
});

router.put('/:id', async (req, res, next) => {
  try {
    const id = parseBookId(req.params.id);

    if (!id) {
      return res.status(400).json({ message: 'Invalid book id' });
    }

    const { book, errors } = validateBookPayload(req.body);

    if (errors.length > 0) {
      return res.status(400).json({ message: 'Invalid book data', errors });
    }

    const db = await connectToDatabase();
    const result = await getBooksCollection(db).findOneAndUpdate(
      { _id: id },
      { $set: book },
      { returnDocument: 'after' },
    );

    if (!result) {
      return res.status(404).json({ message: 'Book not found' });
    }

    return res.json(result);
  } catch (error) {
    return next(error);
  }
});

router.patch('/:id/availability', async (req, res, next) => {
  try {
    const id = parseBookId(req.params.id);

    if (!id) {
      return res.status(400).json({ message: 'Invalid book id' });
    }

    if (typeof req.body.available !== 'boolean') {
      return res.status(400).json({
        message: 'Invalid availability data',
        errors: ['available is required and must be a boolean'],
      });
    }

    const db = await connectToDatabase();
    const result = await getBooksCollection(db).findOneAndUpdate(
      { _id: id },
      { $set: { available: req.body.available } },
      { returnDocument: 'after' },
    );

    if (!result) {
      return res.status(404).json({ message: 'Book not found' });
    }

    return res.json(result);
  } catch (error) {
    return next(error);
  }
});

router.delete('/:id', async (req, res, next) => {
  try {
    const id = parseBookId(req.params.id);

    if (!id) {
      return res.status(400).json({ message: 'Invalid book id' });
    }

    const db = await connectToDatabase();
    const result = await getBooksCollection(db).deleteOne({ _id: id });

    if (result.deletedCount === 0) {
      return res.status(404).json({ message: 'Book not found' });
    }

    return res.status(204).send();
  } catch (error) {
    return next(error);
  }
});

module.exports = router;
