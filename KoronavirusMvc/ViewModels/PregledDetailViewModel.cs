﻿using KoronavirusMvc.Models;
using System.Collections.Generic;

namespace KoronavirusMvc.ViewModels
{
    public class PregledDetailViewModel
    {
        public Pregled Pregled { get; set; }
        public string IdOsoba { get; set; }
        public List<Simptom> Simptomi { get; set; }
        public List<Terapija> Terapije { get; set; }
    }
}