﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KoronavirusMvc.Models;
using Microsoft.Extensions.Options;
using KoronavirusMvc.ViewModels;
using KoronavirusMvc.Extensions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace KoronavirusMvc.Controllers
{
    public class PutovanjeController : Controller
    {
        private readonly RPPP09Context _context;
        private readonly AppSettings _appSettings;
                    

        public PutovanjeController(RPPP09Context context, IOptionsSnapshot<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings.Value;
        }
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await PrepareDropdownLists();
            return View();
        }
        private async Task PrepareDropdownLists()
        {
            var grad = await _context.Lokacija.OrderBy(d => d.ImeGrada).Select(d => new { d.ImeGrada, d.SifraGrada }).ToListAsync();
            ViewBag.Gradovi = new SelectList(grad, nameof(Lokacija.SifraGrada), nameof(Lokacija.ImeGrada));
            var osoba = await _context.Osoba.OrderBy(d => d.Ime).Select(d => new { d.Ime, d.IdentifikacijskiBroj }).ToListAsync();
            ViewBag.Osobe = new SelectList(osoba, nameof(Osoba.IdentifikacijskiBroj), nameof(Osoba.Ime));

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Putovanje putovanje)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(putovanje);
                    _context.SaveChanges();
                    TempData[Constants.Message] = $"Putovanje {putovanje.SifraPutovanja} uspjesno dodano.";
                    TempData[Constants.ErrorOccurred] = false;

                    return RedirectToAction(nameof(Index));

                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.CompleteExceptionMessage());
                    return View(putovanje);
                }
            }
            else
            {
                return View(putovanje);
            }
        }
        public IActionResult Index(int page = 1, int sort = 1, bool ascending = true)
        {
            int pagesize = _appSettings.PageSize;
            //var query = _context.Putovanje.AsNoTracking();
            var query = _context.Putovanje.Include(p => p.IdentifikacijskiBrojNavigation).AsNoTracking();
            int count = query.Count();
            var pagingInfo = new PagingInfo
            {
                CurrentPage = page,
                Sort = sort,
                Ascending = ascending,
                ItemsPerPage = pagesize,
                TotalItems = count
            };

            if(page > pagingInfo.TotalPages)
            {
                return RedirectToAction(nameof(Index), new { page = pagingInfo.TotalPages, sort, ascending} );
            }

            System.Linq.Expressions.Expression<Func<Putovanje, object>> orderSelection = null;
            switch (sort)
            {
                case 1:
                    orderSelection = p => p.DatumPolaska;
                    break;
                case 2:
                    orderSelection = p => p.DatumVracanja;
                    break;
                case 3:
                    orderSelection = p => p.SifraPutovanja;
                    break;
                case 4:
                    orderSelection = p => p.IdentifikacijskiBroj;
                    break;
            }

            if(orderSelection != null)
            {
                query = ascending ? query.OrderBy(orderSelection) : query.OrderByDescending(orderSelection);
            }

            var putovanja = query.Skip((page -1) * pagesize)
                                 .Take(_appSettings.PageSize)
                                 .ToList();
            var model = new PutovanjeViewModel
            {
                Putovanje = putovanja,
                PagingInfo = pagingInfo
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int SifraPutovanja, int page = 1, int sort = 1, bool ascending = true)
        {
            var putovanje = _context.Putovanje.Find(SifraPutovanja);

            if (putovanje == null)
            {
                return NotFound();
            }
            else
            {
                try
                {
                    int naziv = putovanje.SifraPutovanja;
                    _context.Remove(putovanje);
                    _context.SaveChanges();
                    TempData[Constants.Message] = $"Putovanje {naziv} je uspješno obrisano.";
                    TempData[Constants.ErrorOccurred] = false;
                }
                catch (Exception ex)
                {
                    TempData[Constants.Message] = $"Pogreska prilikom brisanja putovanja: " + ex.CompleteExceptionMessage();
                    TempData[Constants.ErrorOccurred] = true;
                }
                return RedirectToAction(nameof(Index), new { page, sort, ascending });
            }

        }
    }
}
