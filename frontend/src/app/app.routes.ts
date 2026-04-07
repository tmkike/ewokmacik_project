import { Routes } from '@angular/router';
import { MainLayout } from './layout/main-layout/main-layout';
import { Home } from './pages/home/home';
import { Books } from './pages/books/books';
import { BookDetail } from './pages/book-detail/book-detail';
import { Contact } from './pages/contact/contact';
import { Mod } from './pages/mod/mod';
import { Add } from './pages/add/add';

export const routes: Routes = [
  {
    path: '',
    component: MainLayout,
    children: [
      { path: '', redirectTo: 'home', pathMatch: 'full' },
      { path: 'home', component: Home },
      { path: 'books', component: Books },
      { path: 'books/:id', component: BookDetail },
      { path: 'contact', component: Contact },
      { path: 'mod', component: Mod },
      { path: 'add', component: Add }
    ]
  },
  { path: '**', redirectTo: 'home' }
];
