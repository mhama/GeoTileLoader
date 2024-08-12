using System;

namespace GeoTile.PlateauStreaming
{
    [Serializable]
    public class DatasetArray
    {
        public Dataset[] datasets;
    }

    [Serializable]
    public class Dataset
    {
        public string id;
        public string name;
        public string pref;
        public string pref_code;
        public string city;
        public string city_code;
        public string ward;
        public string ward_code;
        public string type;
        public string type_en;
        public string url;
        public int year;
        public string format;
        public string lod;
        public bool texture;
    }
}