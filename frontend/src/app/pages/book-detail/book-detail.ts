import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { Book } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-book-detail',
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './book-detail.html',
  styleUrl: './book-detail.scss',
})
export class BookDetail {
  book?: Book;
  bookId = '';
  loading = false;
  saving = false;
  successMessage = '';
  errorMessage = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly bookService: BookService,
  ) {
    const navigationBook = this.router.getCurrentNavigation()?.extras.state?.['book'] as Book | undefined;

    if (navigationBook) {
      this.book = { ...navigationBook };
    }
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');

      if (!id) {
        this.errorMessage = 'Hianyzo konyv azonosito.';
        this.loading = false;
        return;
      }

      this.bookId = id;
      this.loadBook(id);
    });
  }

  loadBook(id: string): void {
    this.loading = !this.book;
    this.errorMessage = '';

    this.bookService.getBook(id).pipe(
      finalize(() => {
        this.loading = false;
      }),
    ).subscribe({
      next: (book) => {
        this.book = book;
      },
      error: () => {
        this.errorMessage = `Nem sikerult betolteni a konyvet. Azonosito: ${id}`;
      },
    });
  }

  saveBook(): void {
    if (!this.book?._id) {
      return;
    }

    this.saving = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.bookService.updateBook(this.book._id, this.book).subscribe({
      next: (updatedBook) => {
        this.book = updatedBook;
        this.successMessage = 'A konyv sikeresen modositva.';
        this.saving = false;
      },
      error: () => {
        this.errorMessage = 'Nem sikerult modositani a konyvet.';
        this.saving = false;
      },
    });
  }

  toggleAvailability(): void {
    if (!this.book?._id) {
      return;
    }

    this.bookService.updateAvailability(this.book._id, !this.book.available).subscribe({
      next: (updatedBook) => {
        this.book = updatedBook;
        this.successMessage = 'Az elerhetoseg sikeresen frissitve.';
      },
      error: () => {
        this.errorMessage = 'Nem sikerult modositani az elerhetoseget.';
      },
    });
  }

  deleteBook(): void {
    if (!this.book?._id || !confirm(`Biztosan torlod ezt a konyvet: ${this.book.title}?`)) {
      return;
    }

    this.bookService.deleteBook(this.book._id).subscribe({
      next: () => {
        void this.router.navigate(['/books']);
      },
      error: () => {
        this.errorMessage = 'Nem sikerult torolni a konyvet.';
      },
    });
  }
}
