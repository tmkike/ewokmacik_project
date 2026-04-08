import { Component } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.scss',
})
export class MainLayout {
  constructor(private readonly router: Router) {}

  openBooks(): void {
    // A friss query paraméter garantálja, hogy a listaoldal alapállapotból induljon.
    void this.router.navigate(['/books'], {
      queryParams: { refresh: Date.now() },
    });
  }
}
