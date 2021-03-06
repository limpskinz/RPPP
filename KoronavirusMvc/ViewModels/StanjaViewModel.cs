﻿using KoronavirusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KoronavirusMvc.ViewModels
{
    public class StanjaViewModel
    {
        public int SifraStanja { get; set; }
        public string NazivStanja { get; set; }
        public IEnumerable<Stanje> Stanja { get; set; }
        public PagingInfo PagingInfo { get; set; }
    }
}
