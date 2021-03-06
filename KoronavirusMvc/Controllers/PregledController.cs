﻿using KoronavirusMvc.Extensions;
using KoronavirusMvc.Models;
using KoronavirusMvc.ViewModels;
using KoronavirusMvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using PdfRpt.Core.Contracts;
using PdfRpt.FluentInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KoronavirusMvc.Controllers
{   
    /// <summary>
    /// Razred za backend rad s pregledima i tablicama vezanim tablicu pregled
    /// Napravio Lovre Gusar
    /// </summary>
    public class PregledController : Controller
    {
        private readonly RPPP09Context ctx;

        private readonly AppSettings appSettings;

        private readonly ILogger<PregledController> logger;

        /// <summary>
        /// Konstruktor razreda PregledController
        /// </summary>
        /// <param name="ctx">kontekst baze</param>
        /// <param name="optionsSnapshot">opcije</param>
        /// <param name="logger">logger za ispis logova prilikom unosa, brisanja i ažuriranja u bazi podataka</param>
        public PregledController(RPPP09Context ctx, IOptionsSnapshot<AppSettings> optionsSnapshot, ILogger<PregledController> logger)
        {
            this.ctx = ctx;
            this.logger = logger;
            appSettings = optionsSnapshot.Value;
        }

        /// <summary>
        /// Metoda za brisanje pregleda iz baze podataka
        /// </summary>
        /// <param name="SifraPregleda">Šifra pregleda koji želimo izbrisati</param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string SifraPregleda, int page = 1, int sort = 1, bool ascending = true)
        {
            var pregled = ctx.Pregled.Find(Int32.Parse(SifraPregleda));
            if (pregled == null)
            {
                return NotFound();
            }
            else
            {
                try
                {
                    ctx.Remove(pregled);
                    ctx.SaveChanges();

                    logger.LogInformation($"Pregled {pregled.SifraPregleda} obrisan.");
                    TempData[Constants.Message] = $"Pregled {pregled.SifraPregleda} uspješno obrisan.";
                    TempData[Constants.ErrorOccurred] = false;
                }
                catch (Exception exc)
                {
                    logger.LogError($"Pogreška prilikom brisanja pregleda {exc.CompleteExceptionMessage()} ");
                    TempData[Constants.Message] = $"Pogreška prilikom brisanja pregleda." + exc.CompleteExceptionMessage();
                    TempData[Constants.ErrorOccurred] = true;
                }
                return RedirectToAction(nameof(Index), new { page, sort, ascending });
            }
        }

        /// <summary>
        /// Metoda za dohvaćanje stranice Create.cshtml za stvaranje novog pregleda
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Create()
        {
            prepareDropDownTerapije();
            prepareDropDownSimptomi();
            return View();
        }

        /// <summary>
        /// Metoda za stvaranje novog pregleda u bazi podataka
        /// </summary>
        /// <param name="pregledCreate">View model koji sadrži pregled i detalje pregleda; simptome i terapije</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PregledCreateViewModel pregledCreate)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if(ctx.Osoba.Find(pregledCreate.OsobaPregled.IdentifikacijskiBroj) != null)
                    {
                        pregledCreate.Pregled.SifraPregleda = (int)NewId();
                        ctx.Add(pregledCreate.Pregled);
                        pregledCreate.OsobaPregled.SifraPregleda = pregledCreate.Pregled.SifraPregleda;
                        ctx.Add(pregledCreate.OsobaPregled);
                        
                        foreach(var opis in pregledCreate.Simptomi)
                        {
                            var simptom = ctx.Simptom.AsNoTracking().Where(p => p.Opis == opis).FirstOrDefault();

                            if (simptom != null)
                            {
                                ctx.Add(new PregledSimptom
                                {
                                    SifraPregleda = pregledCreate.Pregled.SifraPregleda,
                                    SifraSimptoma = simptom.SifraSimptoma
                                });
                            }
                        }

                        foreach (var opis in pregledCreate.Terapije)
                        {
                            var terapija = ctx.Terapija.AsNoTracking().Where(p => p.OpisTerapije == opis).FirstOrDefault();

                            if (terapija != null)
                            {
                                ctx.Add(new PregledTerapija
                                {
                                    SifraPregleda = pregledCreate.Pregled.SifraPregleda,
                                    SifraTerapije = terapija.SifraTerapije
                                });
                            }
                        }

                        ctx.SaveChanges();

                        logger.LogInformation($"Pregled {pregledCreate.Pregled.SifraPregleda} dodan.");
                        TempData[Constants.Message] = $"Pregled {pregledCreate.Pregled.SifraPregleda} uspješno dodan.";
                        TempData[Constants.ErrorOccurred] = false;
                        return RedirectToAction(nameof(Index));
                    }

                    else
                    {
                        logger.LogError($"Ne postoji osoba s tim identifikacijskim brojem.");
                        TempData[Constants.Message] = $"Ne postoji osoba s tim identifikacijskim brojem.";
                        TempData[Constants.ErrorOccurred] = true;
                        prepareDropDownTerapije();
                        prepareDropDownSimptomi();
                        return View(pregledCreate);
                    }
                }
                catch (Exception exc)
                {
                    logger.LogError($"Pogreška prilikom dodavanja pregleda {exc.CompleteExceptionMessage()} ");
                    ModelState.AddModelError(string.Empty, exc.CompleteExceptionMessage());
                    prepareDropDownTerapije();
                    prepareDropDownSimptomi();
                    return View(pregledCreate);
                }
            }
            else
            {
                prepareDropDownTerapije();
                prepareDropDownSimptomi();
                return View(pregledCreate);
            }
        }

        /// <summary>
        /// Metoda za dohvaćanje stranice Edit.cshtml za uređivanje pregleda
        /// </summary>
        /// <param name="id">Šifra pregleda koji želimo urediti</param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Edit(int id, int page = 1, int sort = 1, bool ascending = true)
        {
            var pregled = ctx.Pregled
                             .AsNoTracking()
                             .Where(p => p.SifraPregleda == id)
                             .FirstOrDefault();

            var idOsoba = ctx.OsobaPregled.AsNoTracking()
                             .Where(p => p.SifraPregleda == id)
                             .FirstOrDefault();

            if (pregled == null)
            {
                return NotFound($"Ne postoji pregled s tom šifrom: {id}");
            }
            else
            {
                ViewBag.Page = page;
                ViewBag.Sort = sort;
                ViewBag.ascending = ascending;


                return View(new PregledCreateViewModel { 
                    Pregled = pregled,
                    OsobaPregled = idOsoba
                });
            }
        }

        /// <summary>
        /// Metoda za uređivanje pregleda u bazi podataka
        /// </summary>
        /// <param name="id">Šifra pregleda koji želimo urediti</param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpPost, ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, int page = 1, int sort = 1, bool ascending = true)
        {
            try
            {
                Pregled pregled = await ctx.Pregled.FindAsync(id);
                OsobaPregled osobaPregled = await ctx.OsobaPregled.FindAsync(id);

                PregledCreateViewModel pc = new PregledCreateViewModel
                {
                    Pregled = pregled,
                    OsobaPregled = osobaPregled
                };

                if (pregled == null)
                {
                    return NotFound($"Ne postoji pregled s tom šifrom {id}");
                }

                ViewBag.page = page;
                ViewBag.sort = sort;
                ViewBag.ascending = ascending;
                bool ok = await TryUpdateModelAsync<PregledCreateViewModel>(pc, "", p => p.Pregled, p => p.OsobaPregled);

                if (ok)
                {
                    if (ctx.Osoba.Find(osobaPregled.IdentifikacijskiBroj) != null)
                    {
                        try
                        {
                            TempData[Constants.Message] = $"Pregled {pregled.SifraPregleda} uspješno ažuriran.";
                            TempData[Constants.ErrorOccurred] = false;

                            await ctx.SaveChangesAsync();
                            logger.LogInformation($"Pregled {pregled.SifraPregleda} uspješno ažuriran.");
                            return RedirectToAction(nameof(Index), new { page, sort, ascending });
                        }
                        catch (Exception exc)
                        {
                            logger.LogError($"Pogreška prilikom ažuriranja pregleda {exc.CompleteExceptionMessage()} ");
                            ModelState.AddModelError(string.Empty, exc.CompleteExceptionMessage());
                            return View(pc);
                        }
                    }
                    else
                    {
                        logger.LogError($"Ne postoji osoba s tim identifikacijskim brojem.");
                        ModelState.AddModelError(string.Empty, "Ne postoji osoba s tim identifikacijskim brojem.");
                        return View(pc);
                    }
                }
                else
                {
                    logger.LogError($"Podatke o pregledu nije moguće povezati s forme.");
                    ModelState.AddModelError(string.Empty, "Podatke o pregledu nije moguće povezati s forme.");
                    return View(pc);
                }
            }
            catch (Exception exc)
            {
                TempData[Constants.Message] = exc.CompleteExceptionMessage();
                TempData[Constants.ErrorOccurred] = true;
                logger.LogError($"Pogreška prilikom ažuriranja pregleda {exc.CompleteExceptionMessage()} ");
                return RedirectToAction(nameof(Edit), new { page, sort, ascending });
            }
        }

        /// <summary>
        /// Metoda za tablični ispis svih pregleda
        /// </summary>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        public IActionResult Index(int page = 1, int sort = 1, bool ascending = true)
        {
            int pagesize = appSettings.PageSize;
            var query = ctx.Pregled.AsNoTracking();

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
                return RedirectToAction(nameof(Index), new {
                    page = pagingInfo.TotalPages,
                    sort,
                    ascending
                });
            }

            System.Linq.Expressions.Expression<Func<Pregled, object>> orderSelector = null;
            switch (sort)
            {
                case 1:
                    orderSelector = p => p.SifraPregleda;
                    break;
                case 2:
                    orderSelector = p => p.Datum;
                    break;
                case 3:
                    orderSelector = p => p.Anamneza;
                    break;
                case 4:
                    orderSelector = p => p.Dijagnoza;
                    break;

            }

            if (orderSelector != null)
            {
                query = ascending ? query.OrderBy(orderSelector) : query.OrderByDescending(orderSelector);
            }

            var pregledi = query
                              .Skip((page - 1) * pagesize)
                              .Take(pagesize)
                              .ToList();

            var model = new PreglediViewModel
            {
                Pregledi = pregledi,
                PagingInfo = pagingInfo
            };

            return View(model);
        }

        /// <summary>
        /// Metoda za ispis master details pregleda
        /// </summary>
        /// <param name="id">Šifra pregleda kojeg želimo otvoriti u MD</param>
        /// <returns></returns>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pregled = await ctx.Pregled.FirstOrDefaultAsync(p => p.SifraPregleda == id);

            if (pregled == null)
            {
                return NotFound();
            }

            pregled.PregledSimptom = ctx.PregledSimptom.AsNoTracking()
                                                       .Where(p => p.SifraPregleda == id)
                                                       .ToList();

            List<Simptom> simptomi = new List<Simptom>();

            foreach(PregledSimptom ps in pregled.PregledSimptom)
            {
                var simptom = await ctx.Simptom.FirstOrDefaultAsync(p => p.SifraSimptoma == ps.SifraSimptoma);
                simptomi.Add(simptom);
            }

            pregled.PregledTerapija = ctx.PregledTerapija.AsNoTracking()
                                                       .Where(p => p.SifraPregleda == id)
                                                       .ToList();

            List<Terapija> terapije = new List<Terapija>();

            foreach (PregledTerapija pt in pregled.PregledTerapija)
            {
                var terapija = await ctx.Terapija.FirstOrDefaultAsync(p => p.SifraTerapije == pt.SifraTerapije);
                terapije.Add(terapija);
            }

            var osoba = await ctx.OsobaPregled.FirstOrDefaultAsync(p => p.SifraPregleda == id);

            var model = new PregledDetailViewModel
            {
                Pregled = pregled,
                Simptomi = simptomi,
                Terapije = terapije,
                OsobaPregled = osoba
            };

            return View(model);
        }

        /// <summary>
        /// Metoda za dodavanje liste svih simptoma za kasnije ispisivanje prilikom Edit ili Create pregleda
        /// </summary>
        private void prepareDropDownSimptomi()
        {
            var simptomi = ctx.Simptom.AsNoTracking().ToList();
            List<string> opisi = new List<string>();
            foreach(var simptom in simptomi)
            {
                opisi.Add(simptom.Opis);
            }
            ViewBag.Simptomi = new MultiSelectList(opisi);
        }

        /// <summary>
        /// Metoda za dodavanje liste svih terapija za kasnije ispisivanje prilikom Edit ili Create pregleda
        /// </summary>
        private void prepareDropDownTerapije()
        {
            var terapije = ctx.Terapija.AsNoTracking().ToList();
            List<string> opisi= new List<string>();
            foreach (var terapija in terapije)
            {
                opisi.Add(terapija.OpisTerapije);
            }
            ViewBag.Terapije = new MultiSelectList(opisi);
        }

        /// <summary>
        /// Metoda za uklanjanje simptoma iz detalja pregleda
        /// </summary>
        /// <param name="SifraPregleda">Šifra pregleda koji je master</param>
        /// <param name="SifraSimptoma">Šifra simptoma kojeg želimo ukloniti iz detailsa</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveSimptom(int SifraPregleda, int SifraSimptoma)
        {
            var pregledSimptom = ctx.PregledSimptom.Find(SifraSimptoma, SifraPregleda);

            if (pregledSimptom == null)
            {
                return NotFound();
            }

            else
            {
                try
                {
                    ctx.Remove(pregledSimptom);
                    ctx.SaveChanges();

                    var result = new
                    {
                        message = $"Simptom {pregledSimptom.SifraSimptoma} uspješno uklonjen.",
                        successful = true
                    };
                    logger.LogInformation($"Simptom {pregledSimptom.SifraSimptoma} uspješno uklonjen.");
                    return Json(result);
                }
                catch (Exception exc)
                {
                    var result = new
                    {
                        message = $"Pogreška prilikom uklanjanja simptoma." + exc.CompleteExceptionMessage(),
                        successful = false
                    };
                    logger.LogError($"Pogreška prilikom uklanjanja simptoma. {exc.CompleteExceptionMessage()} ");
                    return Json(result);
                }
            }
        }

        /// <summary>
        /// Metoda za uklanjanje terapija iz detalja pregleda
        /// </summary>
        /// <param name="SifraPregleda">Šifra pregleda koji je master</param>
        /// <param name="SifraTerapije">Šifra terapije koju želimo ukloniti iz detailsa</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveTerapija(int SifraPregleda, int SifraTerapije)
        {
            var pregledTerapija = ctx.PregledTerapija.Find(SifraPregleda, SifraTerapije);

            if (pregledTerapija == null)
            {
                return NotFound();
            }

            else
            {
                try
                {
                    ctx.Remove(pregledTerapija);
                    ctx.SaveChanges();

                    var result = new
                    {
                        message = $"Terapija {pregledTerapija.SifraTerapije} uspješno uklonjena.",
                        successful = true
                    };

                    logger.LogInformation($"Terapija {pregledTerapija.SifraTerapije} uspješno uklonjena.");

                    return Json(result);
                }
                catch (Exception exc)
                {
                    var result = new
                    {
                        message = $"Pogreška prilikom uklanjanja terapije." + exc.CompleteExceptionMessage(),
                        successful = false
                    };

                    logger.LogError($"Pogreška prilikom uklanjanja terapije." + exc.CompleteExceptionMessage());

                    return Json(result);
                }
            }
        }

        /// <summary>
        /// Metoda koja dohvaća stranicu EditDetail.cshtml za MD formu
        /// </summary>
        /// <param name="id">Šifra pregleda koji želimo otvoriti u MD formi</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> EditDetail(int id)
        {;
            prepareDropDownTerapije();
            prepareDropDownSimptomi();

            var pregled = ctx.Pregled
                             .AsNoTracking()
                             .Where(p => p.SifraPregleda == id)
                             .FirstOrDefault();

            var idOsoba = ctx.OsobaPregled.AsNoTracking()
                             .Where(p => p.SifraPregleda == id)
                             .FirstOrDefault();

            pregled.PregledSimptom = ctx.PregledSimptom.AsNoTracking()
                                                       .Where(p => p.SifraPregleda == id)
                                                       .ToList();

            List<Simptom> simptomi = new List<Simptom>();

            foreach (PregledSimptom ps in pregled.PregledSimptom)
            {
                var simptom = await ctx.Simptom.FirstOrDefaultAsync(p => p.SifraSimptoma == ps.SifraSimptoma);
                simptomi.Add(simptom);
            }

            pregled.PregledTerapija = ctx.PregledTerapija.AsNoTracking()
                                                       .Where(p => p.SifraPregleda == id)
                                                       .ToList();

            List<Terapija> terapije = new List<Terapija>();

            foreach (PregledTerapija pt in pregled.PregledTerapija)
            {
                var terapija = await ctx.Terapija.FirstOrDefaultAsync(p => p.SifraTerapije == pt.SifraTerapije);
                terapije.Add(terapija);
            }

            if (pregled == null)
            {
                return NotFound($"Ne postoji pregled s tom šifrom: {id}");
            }
            else
            {
                return View(new PregledDetailViewModel
                {
                    Pregled = pregled,
                    OsobaPregled = idOsoba,
                    Simptomi = simptomi,
                    Terapije = terapije
                });
            }
        }

        /// <summary>
        /// Metoda koja uređuje pregled i sprema ažuriranu MD formu u bazu podataka
        /// </summary>
        /// <param name="id">Šifra pregleda</param>
        /// <returns></returns>
        [HttpPost, ActionName("EditDetail")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDetail(int id)
        {
            try
            {
                Pregled pregled = await ctx.Pregled.FindAsync(id);
                OsobaPregled osobaPregled = await ctx.OsobaPregled.FindAsync(id);

                PregledCreateViewModel pc = new PregledCreateViewModel
                {
                    Pregled = pregled,
                    OsobaPregled = osobaPregled
                };

                if (pregled == null)
                {
                    logger.LogError($"Ne postoji pregled s tom šifrom {id}");
                    return NotFound($"Ne postoji pregled s tom šifrom {id}");
                }

                bool ok = await TryUpdateModelAsync<PregledCreateViewModel>(pc, "", p => p.Pregled, p => p.OsobaPregled);

                if (ok)
                {
                    if (ctx.Osoba.Find(osobaPregled.IdentifikacijskiBroj) != null)
                    {
                        try
                        {
                            TempData[Constants.Message] = $"Pregled {pregled.SifraPregleda} uspješno ažuriran.";
                            TempData[Constants.ErrorOccurred] = false;

                            logger.LogInformation($"Pregled {pregled.SifraPregleda} uspješno ažuriran.");

                            await ctx.SaveChangesAsync();

                            return RedirectToAction(nameof(Details), new { id });
                        }
                        catch (Exception exc)
                        {
                            ModelState.AddModelError(string.Empty, exc.CompleteExceptionMessage());
                            logger.LogError($"Greška prilikom ažuriranja pregleda {exc.CompleteExceptionMessage()}");
                            prepareDropDownTerapije();
                            prepareDropDownSimptomi();
                            return View(pc);
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Ne postoji osoba s tim identifikacijskim brojem.");
                        logger.LogError($"Greška prilikom ažuriranja pregleda. Ne postoji osoba s tim identifikacijskim brojem.");
                        prepareDropDownTerapije();
                        prepareDropDownSimptomi();
                        return View(pc);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Podatke o pregledu nije moguće povezati s forme.");
                    logger.LogError($"Greška prilikom ažuriranja pregleda. Podatke o pregledu nije moguće povezati s forme.");
                    prepareDropDownTerapije();
                    prepareDropDownSimptomi();
                    return View(pc);
                }
            }
            catch (Exception exc)
            {
                TempData[Constants.Message] = exc.CompleteExceptionMessage();
                TempData[Constants.ErrorOccurred] = true;

                logger.LogError($"Greška prilikom ažuriranja pregleda. {exc.CompleteExceptionMessage()}");

                return RedirectToAction(nameof(EditDetail), new { id });
            }
        }

        /// <summary>
        /// Metoda za dodavanje simptoma u detalje pregleda
        /// </summary>
        /// <param name="SifraPregleda">Šifra pregleda koji je master</param>
        /// <param name="simptomi">Lista simptoma koje dodajemo u details</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DodajSimptome(int SifraPregleda, List<string> simptomi)
        {
            foreach (var opis in simptomi)
            {
                var simptom = ctx.Simptom.AsNoTracking().Where(p => p.Opis == opis).FirstOrDefault();

                if (simptom != null)
                {
                    if (ctx.PregledSimptom.Find(simptom.SifraSimptoma, SifraPregleda) == null)
                    {
                        ctx.Add(new PregledSimptom
                        {
                            SifraPregleda = SifraPregleda,
                            SifraSimptoma = simptom.SifraSimptoma
                        });
                    }
                }
                else
                {
                    TempData[Constants.Message] = $"Simptomi nisu dodani.";
                    TempData[Constants.ErrorOccurred] = true;

                    logger.LogError($"Simptomi nisu dodani.");

                    return RedirectToAction(nameof(EditDetail), new { id = SifraPregleda });
                }
            }

            ctx.SaveChanges();

            TempData[Constants.Message] = $"Simptomi uspješno dodani.";
            TempData[Constants.ErrorOccurred] = false;

            logger.LogInformation($"Simptomi uspješno dodani.");

            return RedirectToAction(nameof(EditDetail), new { id = SifraPregleda });
        }

        /// <summary>
        /// Metoda za dodavanje terapija u detalje pregleda
        /// </summary>
        /// <param name="SifraPregleda">Šifra pregleda koji je master</param>
        /// <param name="terapije">Lista terapija koje dodajemo u details</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DodajTerapije(int SifraPregleda, List<string> terapije)
        {
            foreach (var opis in terapije)
            {
                var terapija = ctx.Terapija.AsNoTracking().Where(p => p.OpisTerapije == opis).FirstOrDefault();

                if (terapija != null)
                {
                    if (ctx.PregledTerapija.Find(SifraPregleda, terapija.SifraTerapije) == null) {
                        ctx.Add(new PregledTerapija
                        {
                            SifraPregleda = SifraPregleda,
                            SifraTerapije = terapija.SifraTerapije
                        });
                    }
                }
                else
                {
                    TempData[Constants.Message] = $"Terapije nisu dodane.";
                    TempData[Constants.ErrorOccurred] = true;

                    logger.LogError($"Terapije nisu dodane.");

                    return RedirectToAction(nameof(EditDetail), new { id = SifraPregleda });
                }
            }

            ctx.SaveChanges();

            TempData[Constants.Message] = $"Terapije uspješno dodane.";
            TempData[Constants.ErrorOccurred] = false;

            logger.LogInformation($"Terapije uspješno dodane.");

            return RedirectToAction(nameof(EditDetail), new { id = SifraPregleda });
        }

        /// <summary>
        /// Metoda za generiranje izvješća za MD pregleda.
        /// </summary>
        /// <param name="id">Šifra pregleda za koji se generira MD excel tablica</param>
        public void ExportToExcelDetail(int id)
        {
            var pregled = ctx.Pregled.Find(id);

            pregled.PregledSimptom = ctx.PregledSimptom.AsNoTracking()
                                                       .Where(p => p.SifraPregleda == id)
                                                       .ToList();

            List<Simptom> simptomi = new List<Simptom>();

            foreach (PregledSimptom ps in pregled.PregledSimptom)
            {
                var simptom = ctx.Simptom.FirstOrDefault(p => p.SifraSimptoma == ps.SifraSimptoma);
                simptomi.Add(simptom);
            }

            pregled.PregledTerapija = ctx.PregledTerapija.AsNoTracking()
                                                       .Where(p => p.SifraPregleda == id)
                                                       .ToList();

            List<Terapija> terapije = new List<Terapija>();

            foreach (PregledTerapija pt in pregled.PregledTerapija)
            {
                var terapija = ctx.Terapija.FirstOrDefault(p => p.SifraTerapije == pt.SifraTerapije);
                terapije.Add(terapija);
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            ExcelPackage pck = new ExcelPackage();
            ExcelWorksheet ws = pck.Workbook.Worksheets.Add($"Pregled {pregled.SifraPregleda}");

            ws.Cells["A1"].Value = $"Pregled {pregled.SifraPregleda}";

            ws.Cells["A3"].Value = "Datum";
            ws.Cells["B3"].Value = string.Format("{0:dd MMMM yyyy} at {0:H: mm tt}", DateTimeOffset.Now);

            ws.Cells["A6"].Value = "Sifra Pregleda";
            ws.Cells["B6"].Value = "Datum";
            ws.Cells["C6"].Value = "Anamneza";
            ws.Cells["D6"].Value = "Dijagnoza";

            ws.Cells["A7"].Value = pregled.SifraPregleda;
            ws.Cells["B7"].Value = string.Format("{0:dd MMMM yyyy} at {0:H: mm tt}", pregled.Datum);
            ws.Cells["C7"].Value = pregled.Anamneza;
            ws.Cells["D7"].Value = pregled.Dijagnoza;

            ws.Cells["A10"].Value = "Simptomi";

            int rowStart = 11;
            foreach (Simptom s in simptomi)
            {
                ws.Cells[string.Format("A{0}", rowStart)].Value = s.Opis;
                rowStart++;
            }

            ws.Cells["C10"].Value = "Terapije";

            rowStart = 11;
            foreach (Terapija t in terapije)
            {
                ws.Cells[string.Format("C{0}", rowStart)].Value = t.OpisTerapije;
                rowStart++;
            }

            ws.Cells["A:AZ"].AutoFitColumns();
            Response.Clear();
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.Headers.Add("content-disposition", $"attachment; filename=pregled{pregled.SifraPregleda}.xlsx");
            Response.Body.WriteAsync(pck.GetAsByteArray());
            Response.CompleteAsync();
        }

        /// <summary>
        /// Metoda za generiranje izvješća za Excel. Stvara se excel tablica sa svim pregledima
        /// </summary>
        public void exportToExcel()
        {
            List<PregledExcelViewModel> lista = ctx.Pregled.Select(p => new PregledExcelViewModel
            {
                SifraPregleda = p.SifraPregleda,
                Datum = p.Datum,
                Anamneza = p.Anamneza,
                Dijagnoza = p.Dijagnoza
            }).ToList();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            ExcelPackage pck = new ExcelPackage();
            ExcelWorksheet ws = pck.Workbook.Worksheets.Add("Pregledi");

            ws.Cells["A1"].Value = "Pregledi";

            ws.Cells["A3"].Value = "Datum";
            ws.Cells["B3"].Value = string.Format("{0:dd MMMM yyyy} at {0:H: mm tt}", DateTimeOffset.Now);

            ws.Cells["A6"].Value = "Sifra Pregleda";
            ws.Cells["B6"].Value = "Datum";
            ws.Cells["C6"].Value = "Anamneza";
            ws.Cells["D6"].Value = "Dijagnoza";
            ws.Cells["E6"].Value = "Identifikacijski broj osobe (podatak iz druge tablice)";

            int rowStart = 7;
            foreach (var item in lista)
            {
                var osobaPregled = ctx.OsobaPregled.AsNoTracking().Where(op => op.SifraPregleda == item.SifraPregleda).FirstOrDefault();

                ws.Cells[string.Format("A{0}", rowStart)].Value = item.SifraPregleda;
                ws.Cells[string.Format("B{0}", rowStart)].Value = string.Format("{0:dd MMMM yyyy} at {0:H: mm tt}", item.Datum);
                ws.Cells[string.Format("C{0}", rowStart)].Value = item.Anamneza;
                ws.Cells[string.Format("D{0}", rowStart)].Value = item.Dijagnoza;
                ws.Cells[string.Format("E{0}", rowStart)].Value = osobaPregled.IdentifikacijskiBroj;

                rowStart++;
            }

            ws.Cells["A:AZ"].AutoFitColumns();
            Response.Clear();
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.Headers.Add("content-disposition", "attachment; filename=pregledi.xlsx");
            Response.Body.WriteAsync(pck.GetAsByteArray());
            Response.CompleteAsync();
        }

        /// <summary>
        /// Metoda za generiranje izvješća u pdf formatu. Stvara se tablica sa svim pregledima
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> PDFReport()
        {
            string naslov = "Popis pregleda";
            var pregledi = await ctx.Pregled
                .AsNoTracking()
                .ToListAsync();
            PdfReport report = Constants.CreateBasicReport(naslov);
            report.PagesFooter(footer =>
            {
                footer.DefaultFooter(DateTime.Now.ToString("dd.MM.yyyy."));
            })
            .PagesHeader(header =>
            {
                header.DefaultHeader(defaultHeader =>
                {
                    defaultHeader.RunDirection(PdfRunDirection.LeftToRight);
                    defaultHeader.Message(naslov);
                });
            });
            report.MainTableDataSource(dataSource => dataSource.StronglyTypedList(pregledi));

            report.MainTableColumns(columns =>
            {
                columns.AddColumn(column =>
                {
                    column.PropertyName<Pregled>(o => o.SifraPregleda);
                    column.CellsHorizontalAlignment(HorizontalAlignment.Center);
                    column.IsVisible(true);
                    column.Order(0);
                    column.Width(4);
                    column.HeaderCell("Sifra pregleda", horizontalAlignment: HorizontalAlignment.Center);
                });
                columns.AddColumn(column =>
                {
                    column.PropertyName<Pregled>(o => o.Datum);
                    column.CellsHorizontalAlignment(HorizontalAlignment.Left);
                    column.IsVisible(true);
                    column.Order(1);
                    column.Width(4);
                    column.HeaderCell("Datum", horizontalAlignment: HorizontalAlignment.Left);
                    column.ColumnItemsTemplate(template =>
                    {
                        template.TextBlock();
                        template.DisplayFormatFormula(obj =>
                        {
                            if (obj == null || string.IsNullOrEmpty(obj.ToString()))
                            {
                                return string.Empty;
                            }
                            else
                            {
                                DateTime date = (DateTime)obj;
                                return date.ToString("dd.MM.yyyy");
                            }
                        });
                    });
                });
                columns.AddColumn(column =>
                {
                    column.PropertyName<Pregled>(o => o.Anamneza);
                    column.CellsHorizontalAlignment(HorizontalAlignment.Center);
                    column.IsVisible(true);
                    column.Order(2);
                    column.Width(2);
                    column.HeaderCell("Anamneza", horizontalAlignment: HorizontalAlignment.Center);
                });
                columns.AddColumn(column =>
                {
                    column.PropertyName<Pregled>(o => o.Dijagnoza);
                    column.CellsHorizontalAlignment(HorizontalAlignment.Left);
                    column.IsVisible(true);
                    column.Order(3);
                    column.Width(4);
                    column.HeaderCell("Dijagnoza", horizontalAlignment: HorizontalAlignment.Left);
                });
            });


            byte[] pdf = report.GenerateAsByteArray();

            if (pdf != null)
            {
                Response.Headers.Add("content-disposition", "inline; filename=pregledi.pdf");
                return File(pdf, "application/pdf");
            }
            else
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Pomoćna funkcija za generiranje nove šifre pregleda kad se stvara novi pregled. Novi pregled dobiva prvi najveći slobodan broj.
        /// </summary>
        /// <returns>Šifra novog pregleda</returns>
        private decimal NewId()
        {
            var maxId = ctx.Pregled
                      .Select(o => o.SifraPregleda)
                      .ToList()
                      .Max();

            return maxId + 1;
        }
    }
}
