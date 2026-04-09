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
    if (!this.selectedBook?._id) {
      return;
    }

    this.saving = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.bookService.updateBook(this.selectedBook._id, this.selectedBook).pipe(
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

    this.bookService.updateAvailability(book._id, !book.available).subscribe({
      next: (updatedBook) => {
        this.books = this.books.map((item) => item._id === updatedBook._id ? updatedBook : item);

        if (this.selectedBook?._id === updatedBook._id) {
          this.selectedBook = { ...updatedBook };
        }
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

    this.bookService.deleteBook(book._id).subscribe({
      next: () => {
        this.books = this.books.filter((item) => item._id !== book._id);

        if (this.selectedBook?._id === book._id) {
          this.selectedBook = undefined;
        }

        this.successMessage = 'A könyv sikeresen törölve.';
      },
      error: () => {
        this.errorMessage = 'Nem sikerült törölni a könyvet.';
      },
    });
  }
}
