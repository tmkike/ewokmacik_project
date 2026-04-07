import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

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

    this.bookService.getBooks().subscribe({
      next: (books) => {
        this.books = books;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Nem sikerult betolteni a konyveket.';
        this.loading = false;
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

    this.bookService.updateBook(this.selectedBook._id, this.selectedBook).subscribe({
      next: (updatedBook) => {
        this.books = this.books.map((book) => book._id === updatedBook._id ? updatedBook : book);
        this.selectedBook = { ...updatedBook };
        this.successMessage = 'A konyv sikeresen modositva.';
        this.saving = false;
      },
      error: () => {
        this.errorMessage = 'Nem sikerult modositani a konyvet.';
        this.saving = false;
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
        this.errorMessage = 'Nem sikerult modositani az elerhetoseget.';
      },
    });
  }

  deleteBook(book: Book): void {
    if (!book._id || !confirm(`Biztosan torlod ezt a konyvet: ${book.title}?`)) {
      return;
    }

    this.bookService.deleteBook(book._id).subscribe({
      next: () => {
        this.books = this.books.filter((item) => item._id !== book._id);
        if (this.selectedBook?._id === book._id) {
          this.selectedBook = undefined;
        }
        this.successMessage = 'A konyv sikeresen torolve.';
      },
      error: () => {
        this.errorMessage = 'Nem sikerult torolni a konyvet.';
      },
    });
  }
}
