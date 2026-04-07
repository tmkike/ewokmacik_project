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
  const book = normalizeBookPayload(payload);

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'title')) {
    if (!book.title || typeof book.title !== 'string') {
      errors.push('title is required and must be a non-empty string');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'author')) {
    if (!book.author || typeof book.author !== 'string') {
      errors.push('author is required and must be a non-empty string');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'year')) {
    if (!Number.isInteger(book.year) || book.year < 0) {
      errors.push('year is required and must be a positive integer');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'genre')) {
    if (!book.genre || typeof book.genre !== 'string') {
      errors.push('genre is required and must be a non-empty string');
    }
  }

  if (!partial || Object.prototype.hasOwnProperty.call(payload, 'available')) {
    if (typeof book.available !== 'boolean') {
      errors.push('available is required and must be a boolean');
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
