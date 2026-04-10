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
        void this.router.navigate(['/books'], {
          queryParams: { refresh: Date.now() },
          state: { systemMessage: '\u0041\u0020\u006b\u0102\u00b6\u006e\u0079\u0076\u0020\u0068\u006f\u007a\u007a\u0102\u02c7\u0061\u0064\u0102\u02c7\u0073\u0061\u0020\u0073\u0069\u006b\u0065\u0072\u0065\u0073\u002e' },
        });
      },
      error: () => {
        this.errorMessage = '\u004e\u0065\u006d\u0020\u0073\u0069\u006b\u0065\u0072\u0102\u013d\u006c\u0074\u0020\u0068\u006f\u007a\u007a\u0102\u02c7\u0061\u0064\u006e\u0069\u0020\u0061\u0020\u006b\u0102\u00b6\u006e\u0079\u0076\u0065\u0074\u002e';
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
