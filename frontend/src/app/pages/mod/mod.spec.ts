import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Mod } from './mod';

describe('Mod', () => {
  let component: Mod;
  let fixture: ComponentFixture<Mod>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Mod],
    }).compileComponents();

    fixture = TestBed.createComponent(Mod);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
