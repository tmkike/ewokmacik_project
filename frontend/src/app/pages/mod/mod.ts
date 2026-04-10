import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { Book } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-mod',
  imports: [CommonModule, FormsModule],
  templateUrl: './mod.html',
  styleUrl: './mod.scss',
})
export class Mod {
  books: Book[] = [];
  selectedBook?: Book;
  loading = false;
  saving = false;
  successMessage = '';
  errorMessage = '';

  constructor(private readonly bookService: BookService) {}

  ngOnInit(): void {
    this.loadBooks();
  }

  loadBooks(): void {
    this.loading = true;
    this.errorMessage = '';

    this.bookService.getBooks({ page: 1, pageSize: 50 }).pipe(
      finalize(() => {
        this.loading = false;
      }),
    ).subscribe({
      next: (response) => {
        this.books = response.items;
      },
      error: () => {
        this.errorMessage = 'Nem sikerült betölteni a könyveket.';
      },
    });
  }

  selectBook(book: Book): void {
    this.selectedBook = { ...book };
    this.successMessage = '';
    this.errorMessage = '';
  }

  saveBook(): void {
    const selectedBook = this.selectedBook;

    if (!selectedBook?._id) {
      return;
    }

    this.saving = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.bookService.updateBook(selectedBook._id, selectedBook).pipe(
      finalize(() => {
        this.saving = false;
      }),
    ).subscribe({
      next: (updatedBook) => {
        this.books = this.books.map((book) => book._id === updatedBook._id ? updatedBook : book);
        this.selectedBook = { ...updatedBook };
        this.successMessage = 'A könyv sikeresen módosítva.';
      },
      error: () => {
        this.errorMessage = 'Nem sikerült módosítani a könyvet.';
      },
    });
  }

  toggleAvailability(book: Book): void {
    if (!book._id) {
      return;
    }

    const bookId = book._id;

    this.bookService.updateAvailability(bookId, !book.available).subscribe({
      next: (updatedBook) => {
        this.books = this.books.map((item) => item._id === updatedBook._id ? updatedBook : item);
        this.selectedBook = this.selectedBook?._id === bookId ? { ...updatedBook } : this.selectedBook;
      },
      error: () => {
        this.errorMessage = 'Nem sikerült módosítani az elérhetőséget.';
      },
    });
  }

  deleteBook(book: Book): void {
    if (!book._id || !confirm(`Biztosan törlöd ezt a könyvet: ${book.title}?`)) {
      return;
    }

    const bookId = book._id;

    this.bookService.deleteBook(bookId).subscribe({
      next: () => {
        this.books = this.books.filter((item) => item._id !== bookId);

        this.selectedBook = this.selectedBook?._id === bookId ? undefined : this.selectedBook;

        this.successMessage = 'A könyv sikeresen törölve.';
      },
      error: () => {
        this.errorMessage = 'Nem sikerült törölni a könyvet.';
      },
    });
  }
}
