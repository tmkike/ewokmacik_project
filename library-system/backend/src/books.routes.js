const express = require('express');
const { ObjectId } = require('mongodb');

const { validateBookPayload } = require('./book-validation');
const { connectToDatabase } = require('./db');

const router = express.Router();

function getBooksCollection(db) {
  return db.collection('books');
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function getTrimmedQueryValue(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function createContainsRegex(value) {
  return new RegExp(escapeRegExp(value), 'i');
}

function createExactRegex(value) {
  return new RegExp(`^${escapeRegExp(value)}$`, 'i');
}

function buildBooksFilter(query) {
  const filter = {};
  const search = getTrimmedQueryValue(query.search);
  const title = getTrimmedQueryValue(query.title);
  const author = getTrimmedQueryValue(query.author);
  const genre = getTrimmedQueryValue(query.genre);

  // Az üres vagy szóközös query paraméterek ne szűrjék ki tévesen az összes találatot.
  if (search) {
    const searchRegex = createContainsRegex(search);
    filter.$or = [
      { title: searchRegex },
      { author: searchRegex },
      { genre: searchRegex },
    ];
  }

  if (title) {
    filter.title = createContainsRegex(title);
  }

  if (author) {
    filter.author = createContainsRegex(author);
  }

  if (genre) {
    filter.genre = createExactRegex(genre);
  }

  if (query.available !== undefined && query.available !== '') {
    if (query.available !== 'true' && query.available !== 'false') {
      return {
        error: 'Az elérhetőség szűrője csak true vagy false lehet.',
      };
    }

    filter.available = query.available === 'true';
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
      return res.status(400).json({ message: 'Érvénytelen könyvazonosító.' });
    }

    const db = await connectToDatabase();
    const book = await getBooksCollection(db).findOne({ _id: id });

    if (!book) {
      return res.status(404).json({ message: 'A könyv nem található.' });
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
      return res.status(400).json({ message: 'Érvénytelen könyvadatok.', errors });
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
      return res.status(400).json({ message: 'Érvénytelen könyvazonosító.' });
    }

    const { book, errors } = validateBookPayload(req.body);

    if (errors.length > 0) {
      return res.status(400).json({ message: 'Érvénytelen könyvadatok.', errors });
    }

    const db = await connectToDatabase();
    const updatedBook = await getBooksCollection(db).findOneAndUpdate(
      { _id: id },
      { $set: book },
      {
        returnDocument: 'after',
        includeResultMetadata: false,
      },
    );

    if (!updatedBook) {
      return res.status(404).json({ message: 'A könyv nem található.' });
    }

    return res.json(updatedBook);
  } catch (error) {
    return next(error);
  }
});

router.patch('/:id/availability', async (req, res, next) => {
  try {
    const id = parseBookId(req.params.id);

    if (!id) {
      return res.status(400).json({ message: 'Érvénytelen könyvazonosító.' });
    }

    if (typeof req.body?.available !== 'boolean') {
      return res.status(400).json({
        message: 'Érvénytelen elérhetőségi adat.',
        errors: ['Az available mező kötelező, és logikai értéknek kell lennie.'],
      });
    }

    const db = await connectToDatabase();
    const updatedBook = await getBooksCollection(db).findOneAndUpdate(
      { _id: id },
      { $set: { available: req.body.available } },
      {
        returnDocument: 'after',
        includeResultMetadata: false,
      },
    );

    if (!updatedBook) {
      return res.status(404).json({ message: 'A könyv nem található.' });
    }

    return res.json(updatedBook);
  } catch (error) {
    return next(error);
  }
});

router.delete('/:id', async (req, res, next) => {
  try {
    const id = parseBookId(req.params.id);

    if (!id) {
      return res.status(400).json({ message: 'Érvénytelen könyvazonosító.' });
    }

    const db = await connectToDatabase();
    const result = await getBooksCollection(db).deleteOne({ _id: id });

    if (result.deletedCount === 0) {
      return res.status(404).json({ message: 'A könyv nem található.' });
    }

    return res.status(204).send();
  } catch (error) {
    return next(error);
  }
});

module.exports = router;
