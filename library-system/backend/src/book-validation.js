function isPlainObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function normalizeBookPayload(payload) {
  return {
    title: typeof payload.title === 'string' ? payload.title.trim() : payload.title,
    author: typeof payload.author === 'string' ? payload.author.trim() : payload.author,
    year: payload.year,
    genre: typeof payload.genre === 'string' ? payload.genre.trim() : payload.genre,
    available: payload.available,
  };
}

function validateBookPayload(payload, { partial = false } = {}) {
  const errors = [];

  if (!isPlainObject(payload)) {
    return {
      book: normalizeBookPayload({}),
      errors: ['A kérés törzsének JSON objektumnak kell lennie.'],
    };
  }

  const book = normalizeBookPayload(payload);

  // Csak azokat a mezőket kérjük számon, amelyeket a kérésnek valóban tartalmaznia kell.
  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'title')) {
    if (!book.title || typeof book.title !== 'string') {
      errors.push('A cím kötelező, és nem lehet üres.');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'author')) {
    if (!book.author || typeof book.author !== 'string') {
      errors.push('A szerző megadása kötelező, és nem lehet üres.');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'year')) {
    if (!Number.isInteger(book.year) || book.year < 0) {
      errors.push('A kiadás éve kötelező, és nemnegatív egész számnak kell lennie.');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'genre')) {
    if (!book.genre || typeof book.genre !== 'string') {
      errors.push('A kategória megadása kötelező, és nem lehet üres.');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'available')) {
    if (typeof book.available !== 'boolean') {
      errors.push('Az elérhetőség megadása kötelező, és logikai értéknek kell lennie.');
    }
  }

  return {
    book,
    errors,
  };
}

module.exports = {
  validateBookPayload,
};
