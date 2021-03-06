﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KoronavirusMvc.Extensions;
using KoronavirusMvc.Models;
using KoronavirusMvc.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KoronavirusMvc.Controllers
{
    public class DrzavaController : Controller
    {
        private readonly RPPP09Context _context;
        private readonly AppSettings _appSettings;

        /// <summary>
        /// ovdje se definiraju konteksti apliakcije s kojima se radi i postavke stranicenja
        /// </summary>
        /// <param name="context"></param>
        /// <param name="appSettings"></param>
        public DrzavaController(RPPP09Context context, IOptionsSnapshot<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings.Value;
        }

        /// <summary>
        /// stvaranje, odnosni primitak drzava, ne koristimo
        /// </summary>
        /// <returns></returns>
        // GET: Drzava/Create
        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        // POST: Drzava/Create
        /// <summary>
        /// stvaranje nove drzave
        /// </summary>
        /// <param name="drzava"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Drzava drzava)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(drzava);
                    _context.SaveChanges();
                    TempData[Constants.Message] = $"Država {drzava.SifraDrzave} uspjesno dodano.";
                    TempData[Constants.ErrorOccurred] = false;

                    return RedirectToAction(nameof(Index));

                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.CompleteExceptionMessage());
                    return View(drzava);
                }
            }
            else
            {
                return View(drzava);
            }
        }


        // GET: Drzava/Edit/5
        /// <summary>
        ///primanje podataka za urediti od drzave
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Edit(string id, int page = 1, int sort = 1, bool ascending = true)
        {
            var drzava = _context.Drzava
                .AsNoTracking()
                .Where(d => d.SifraDrzave == id)
                .FirstOrDefault();

            if (drzava == null)
            {
                return NotFound($"Ne postoji drzava s tom šifrom: {id}");
            }
            else
            {
                ViewBag.Page = page;
                ViewBag.Sort = sort;
                ViewBag.ascending = ascending;
                return View(drzava);
            }
        }

        /// <summary>
        /// promjena danih podataka o drzavi 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(string id, int page = 1, int sort = 1, bool ascending = true)
        {
            try
            {
                Drzava drzava = await _context.Drzava.FindAsync(id);
                if (drzava == null)
                {
                    return NotFound($"Ne postoji drzava s identifikacijskom oznakom {id}");
                }

                ViewBag.Page = page;
                ViewBag.Sort = sort;
                ViewBag.Ascending = ascending;
                bool ok = await TryUpdateModelAsync<Drzava>(drzava, "", z => z.ImeDrzave);
                if (ok)
                {
                    try
                    {
                        TempData[Constants.Message] = $"Podaci o državi uspješno su ažurirani.";
                        TempData[Constants.ErrorOccurred] = false;
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index), new { page, sort, ascending });
                    }
                    catch (Exception exc)
                    {
                        ModelState.AddModelError(string.Empty, exc.CompleteExceptionMessage());
                        return View(drzava);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Podatke o drzavi nije moguće povezati s forme");
                    return View(drzava);
                }
            }
            catch (Exception exc)
            {
                TempData[Constants.Message] = exc.CompleteExceptionMessage();
                TempData[Constants.ErrorOccurred] = true;

                return RedirectToAction(nameof(Edit), new { id, page, sort, ascending });
            }
        }

            // GET: Drzava/Delete/5
            public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: Drzava/Delete/5
        /// <summary>
        /// brisanje neke drzave
        /// </summary>
        /// <param name="SifraDrzave"></param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string SifraDrzave, int page = 1, int sort = 1, bool ascending = true)
        {
                var drzava = _context.Drzava.Find(SifraDrzave);
                if(drzava == null)
                {
                    return NotFound();
                }
                else
                {
                    try
                    {
                    string naziv = drzava.ImeDrzave;
                    _context.Remove(drzava);
                    _context.SaveChanges();
                    TempData[Constants.Message] = $"Država {naziv} je uspješno pobrisana.";
                    TempData[Constants.ErrorOccurred] = false;
                    }
                    catch(Exception ex)
                    {
                    TempData[Constants.Message] = $"Pogreska prilikom brisanja drzave: " + ex.CompleteExceptionMessage();
                    TempData[Constants.ErrorOccurred] = true;
                    }
                return RedirectToAction(nameof(Index), new {page, sort, ascending });
                }

        }

        /// <summary>
        /// pomocu ove funkcije omogucavamo stranicenje i tablicu sa vise mogučnosti organizacije podataka, 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        public IActionResult Index(int page = 1, int sort = 1, bool ascending = true)
        {
            int pagesize = _appSettings.PageSize;
            //var query = _context.Putovanje.AsNoTracking();
            var query = _context.Drzava.AsNoTracking();
            int count = query.Count();
            var pagingInfo = new PagingInfo
            {
                CurrentPage = page,
                Sort = sort,
                Ascending = ascending,
                ItemsPerPage = pagesize,
                TotalItems = count
            };

            if (page > pagingInfo.TotalPages)
            {
                return RedirectToAction(nameof(Index), new { page = pagingInfo.TotalPages, sort, ascending });
            }

            System.Linq.Expressions.Expression<Func<Drzava, object>> orderSelection = null;
            switch (sort)
            {
                case 1:
                    orderSelection = d => d.SifraDrzave;
                    break;
                case 2:
                    orderSelection = d => d.ImeDrzave;
                    break;

            }

            if (orderSelection != null)
            {
                query = ascending ? query.OrderBy(orderSelection) : query.OrderByDescending(orderSelection);
            }

            var drzave = query.Skip((page - 1) * pagesize)
                                 .Take(_appSettings.PageSize)
                                 .ToList();
            var model = new DrzavaViewModel
            {
                Drzava = drzave,
                PagingInfo = pagingInfo
            };
            return View(model);
        }
    }
}