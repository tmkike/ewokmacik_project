import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';

import { Book } from '../../models/book';
import { BookService } from '../../services/book.service';

@Component({
  selector: 'app-add',
  imports: [CommonModule, FormsModule],
  templateUrl: './add.html',
  styleUrl: './add.scss',
})
export class Add {
  book: Book = this.createEmptyBook();
  saving = false;
  errorMessage = '';

  constructor(
    private readonly bookService: BookService,
    private readonly router: Router,
  ) {}

  addBook(): void {
    this.saving = true;
    this.errorMessage = '';

    this.bookService.addBook(this.book).pipe(
      finalize(() => {
        this.saving = false;
      }),
    ).subscribe({
      next: () => {
        // The list page consumes this state as a one-time flash message after redirect.
        void this.router.navigate(['/books'], {
          queryParams: { refresh: Date.now() },
          state: { systemMessage: 'A könyv hozzáadása sikeres.' },
        });
      },
      error: () => {
        this.errorMessage = 'Nem sikerült hozzáadni a könyvet.';
      },
    });
  }

  private createEmptyBook(): Book {
    return {
      title: '',
      author: '',
      year: new Date().getFullYear(),
      genre: '',
      available: true,
    };
  }
}
