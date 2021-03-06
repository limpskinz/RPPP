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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using PdfRpt.Core.Contracts;
using PdfRpt.FluentInterface;

namespace KoronavirusMvc.Controllers
{
    /// <summary>
    /// Razred za backend rad sa terapijama
    /// Napravio Lovre Gusar
    /// </summary>
    public class TerapijaController : Controller
    {
        private readonly RPPP09Context ctx;

        private readonly AppSettings appSettings;

        private readonly ILogger<TerapijaController> logger;

        /// <summary>
        /// Konstruktor razreda TerapijaController
        /// </summary>
        /// <param name="ctx">kontekst baze</param>
        /// <param name="optionsSnapshot">opcije</param>
        /// <param name="logger">logger za ispis logova prilikom unosa, brisanja i ažuriranja u bazi podataka</param>
        public TerapijaController(RPPP09Context ctx, IOptionsSnapshot<AppSettings> optionsSnapshot, ILogger<TerapijaController> logger)
        {
            this.ctx = ctx;
            this.logger = logger;
            appSettings = optionsSnapshot.Value;
        }

        /// <summary>
        /// Metoda za tablični ispis svih terapija
        /// </summary>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        public IActionResult Index(int page = 1, int sort = 1, bool ascending = true)
        {
            int pagesize = appSettings.PageSize;
            var query = ctx.Terapija.AsNoTracking();

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
                return RedirectToAction(nameof(Index), new
                {
                    page = pagingInfo.TotalPages,
                    sort,
                    ascending
                });
            }

            System.Linq.Expressions.Expression<Func<Terapija, object>> orderSelector = null;
            switch (sort)
            {
                case 1:
                    orderSelector = s => s.SifraTerapije;
                    break;
                case 2:
                    orderSelector = s => s.OpisTerapije;
                    break;
            }

            if (orderSelector != null)
            {
                query = ascending ? query.OrderBy(orderSelector) : query.OrderByDescending(orderSelector);
            }

            var terapije = query
                              .Skip((page - 1) * pagesize)
                              .Take(pagesize)
                              .ToList();

            var model = new TerapijeViewModel
            {
                Terapije = terapije,
                PagingInfo = pagingInfo
            };

            return View(model);
        }

        /// <summary>
        /// Metoda za dohvaćanje stranice Create.cshtml za stvaranje nove terapije
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// Metoda za stvaranje nove terapije u bazi podataka
        /// </summary>
        /// <param name="terapija">Model terapija</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Terapija terapija)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    terapija.SifraTerapije = (int)NewId();
                    ctx.Add(terapija);
                    ctx.SaveChanges();

                    TempData[Constants.Message] = $"Terapija {terapija.SifraTerapije} uspješno dodana.";
                    TempData[Constants.ErrorOccurred] = false;

                    logger.LogInformation($"Terapija {terapija.SifraTerapije} uspješno dodana.");

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception exc)
                {
                    ModelState.AddModelError(string.Empty, exc.CompleteExceptionMessage());
                    logger.LogError($"Pogreška prilikom dodavanja nove terapije {exc.CompleteExceptionMessage()}");
                    return View(terapija);
                }
            }
            else
            {
                logger.LogError($"Pogreška prilikom dodavanja nove terapije");
                return View(terapija);
            }
        }

        /// <summary>
        /// Metoda za brisanje terapije iz baze podataka
        /// </summary>
        /// <param name="SifraTerapije">Šifra terapije koju želimo izbrisati</param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int SifraTerapije, int page = 1, int sort = 1, bool ascending = true)
        {
            var terapija = ctx.Terapija.Find(SifraTerapije);
            if (terapija == null)
            {
                return NotFound();
            }
            else
            {
                try
                {
                    ctx.Remove(terapija);
                    ctx.SaveChanges();

                    TempData[Constants.Message] = $"Terapija {terapija.SifraTerapije} uspješno obrisana.";
                    logger.LogInformation($"Terapija {terapija.SifraTerapije} uspješno obrisana.");
                    TempData[Constants.ErrorOccurred] = false;
                }
                catch (Exception exc)
                {
                    TempData[Constants.Message] = $"Pogreška prilikom brisanja terapije." + exc.CompleteExceptionMessage();
                    logger.LogError($"Pogreška prilikom brisanja terapije." + exc.CompleteExceptionMessage());
                    TempData[Constants.ErrorOccurred] = true;
                }
                return RedirectToAction(nameof(Index), new { page, sort, ascending });
            }
        }

        /// <summary>
        /// Metoda za dohvaćanje stranice Edit.cshtml za uređivanje terapije
        /// </summary>
        /// <param name="id">Šifra terapije koju želimo urediti</param>
        /// <param name="page"></param>
        /// <param name="sort"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Edit(int id, int page = 1, int sort = 1, bool ascending = true)
        {
            var terapija = ctx.Terapija
                             .AsNoTracking()
                             .Where(p => p.SifraTerapije == id)
                             .FirstOrDefault();

            if (terapija == null)
            {
                return NotFound($"Ne postoji terapija s tom šifrom: {id}");
            }
            else
            {
                ViewBag.Page = page;
                ViewBag.Sort = sort;
                ViewBag.ascending = ascending;
                return View(terapija);
            }
        }

        /// <summary>
        /// Metoda za uređivanje terapije u bazi podataka
        /// </summary>
        /// <param name="id">Šifra terapije koju želimo urediti</param>
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
                Terapija terapija = await ctx.Terapija.FindAsync(id);

                if (terapija == null)
                {
                    logger.LogError($"Pogreška prilikom ažuriranja terapije. Ne postoji terapija s tom šifrom: {id}");
                    return NotFound($"Ne postoji terapija s tom šifrom {id}");
                }

                ViewBag.page = page;
                ViewBag.sort = sort;
                ViewBag.ascending = ascending;
                bool ok = await TryUpdateModelAsync<Terapija>(terapija, "", p => p.OpisTerapije);

                if (ok)
                {
                    try
                    {
                        TempData[Constants.Message] = $"Terapija {terapija.SifraTerapije} uspješno ažurirana.";
                        TempData[Constants.ErrorOccurred] = false;

                        logger.LogInformation($"Terapija {terapija.SifraTerapije} uspješno ažurirana.");

                        await ctx.SaveChangesAsync();

                        return RedirectToAction(nameof(Index), new { page, sort, ascending });
                    }
                    catch (Exception exc)
                    {
                        ModelState.AddModelError(string.Empty, exc.CompleteExceptionMessage());
                        logger.LogError($"Pogreška prilikom ažuriranja terapije. {exc.CompleteExceptionMessage()}");
                        return View(terapija);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Podatke o terapiji nije moguće povezati s forme.");
                    logger.LogError($"Pogreška prilikom ažuriranja terapije. Podatke o terapiji nije moguće povezati s forme.");
                    return View(terapija);
                }
            }
            catch (Exception exc)
            {
                TempData[Constants.Message] = exc.CompleteExceptionMessage();
                logger.LogError($"Pogreška prilikom ažuriranja terapije. {exc.CompleteExceptionMessage()}");
                TempData[Constants.ErrorOccurred] = true;

                return RedirectToAction(nameof(Edit), new { page, sort, ascending });
            }
        }

        /// <summary>
        /// Metoda za generiranje izvješća za Excel. Stvara se excel tablica sa svim terapijama
        /// </summary>
        public void exportToExcel()
        {
            List<TerapijaExcelViewModel> lista = ctx.Terapija.Select(t => new TerapijaExcelViewModel
            {
                SifraTerapije = t.SifraTerapije,
                OpisTerapije = t.OpisTerapije
            }).ToList();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            ExcelPackage pck = new ExcelPackage();
            ExcelWorksheet ws = pck.Workbook.Worksheets.Add("Terapije");

            ws.Cells["A1"].Value = "Terapije";

            ws.Cells["A3"].Value = "Datum";
            ws.Cells["B3"].Value = string.Format("{0:dd MMMM yyyy} at {0:H: mm tt}", DateTimeOffset.Now);

            ws.Cells["A6"].Value = "Sifra terapije";
            ws.Cells["B6"].Value = "Opis terapije";

            int rowStart = 7;
            foreach (var item in lista)
            {
                ws.Cells[string.Format("A{0}", rowStart)].Value = item.SifraTerapije;
                ws.Cells[string.Format("B{0}", rowStart)].Value = item.OpisTerapije;
                rowStart++;
            }

            ws.Cells["A:AZ"].AutoFitColumns();
            Response.Clear();
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.Headers.Add("content-disposition", "attachment; filename=terapije.xlsx");
            Response.Body.WriteAsync(pck.GetAsByteArray());
            Response.CompleteAsync();
        }

        /// <summary>
        /// Metoda za generiranje izvješća u pdf formatu. Stvara se tablica sa svim simptomima
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> PDFReport()
        {
            string naslov = "Popis terapija";
            var terapije = await ctx.Terapija
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
            report.MainTableDataSource(dataSource => dataSource.StronglyTypedList(terapije));

            report.MainTableColumns(columns =>
            {
                columns.AddColumn(column =>
                {
                    column.PropertyName<Terapija>(o => o.SifraTerapije);
                    column.CellsHorizontalAlignment(HorizontalAlignment.Center);
                    column.IsVisible(true);
                    column.Order(0);
                    column.Width(4);
                    column.HeaderCell("Sifra terapije", horizontalAlignment: HorizontalAlignment.Center);
                });
                columns.AddColumn(column =>
                {
                    column.PropertyName<Terapija>(o => o.OpisTerapije);
                    column.CellsHorizontalAlignment(HorizontalAlignment.Left);
                    column.IsVisible(true);
                    column.Order(1);
                    column.Width(4);
                    column.HeaderCell("Opis terapije", horizontalAlignment: HorizontalAlignment.Left);
                });
            });


            byte[] pdf = report.GenerateAsByteArray();

            if (pdf != null)
            {
                Response.Headers.Add("content-disposition", "inline; filename=terapije.pdf");
                return File(pdf, "application/pdf");
            }
            else
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Pomoćna funkcija za generiranje nove šifre terapije kad se stvara nova terapija. Novi terapija dobiva prvi najveći slobodan broj.
        /// </summary>
        /// <returns>Šifra nove terapije</returns>
        private decimal NewId()
        {
            var maxId = ctx.Terapija
                      .Select(o => o.SifraTerapije)
                      .ToList()
                      .Max();

            return maxId + 1;
        }
    }
}
