﻿using KoronavirusMvc.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KoronavirusMvc.ViewModels
{
    public class OsobeViewModel
    {
        public IEnumerable<Osoba> Osobe { get; set; }
        public PagingInfo PagingInfo { get; set; }
    }
}
