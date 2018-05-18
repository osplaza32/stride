﻿/* Main Page */
using System;
using System.Collections.Generic;
using Xamarin.Forms;
using Xamarin.Forms.GoogleMaps;
using BreatheKlere.REST;
using Plugin.Permissions;
using Plugin.Permissions.Abstractions;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BreatheKlere
{
    public partial class BreatheKlerePage : ContentPage
    {
        // mode variables
        public byte mapMode = 0;
        bool isFirstLaunch;
        public byte isHomeSet = 0, isDestinationSet = 0;
        string[] modes = { "bicycling", "walking",  };
        string[] mqModes = { "bicycle", "pedestrian" };
        float maxPollution = 0;
        int mode = 0;

        RESTService rest;

        //location variables
        public string origin, destination, currentPos;
        public Position originPos, destinationPos;

        // Point array 

        Pin startPin, endPin, hotspotPin;
        Position hotspot;
        float peak = 0;

        public BreatheKlerePage()
        {
            InitializeComponent();
            // MapTypes
            var mapTypeValues = new List<MapType>();
            rest = new RESTService();
            foreach (var mapType in Enum.GetValues(typeof(MapType)))
            {
                mapTypeValues.Add((MapType)mapType);
            }
            isFirstLaunch = true;
            map.MapType = mapTypeValues[0];
            map.MyLocationEnabled = true;
            map.IsTrafficEnabled = true;
            map.IsIndoorEnabled = false;
            map.UiSettings.CompassEnabled = true;
            map.UiSettings.RotateGesturesEnabled = true;
            map.UiSettings.MyLocationButtonEnabled = true;
            map.UiSettings.IndoorLevelPickerEnabled = false;
            map.UiSettings.ScrollGesturesEnabled = true;
            map.UiSettings.TiltGesturesEnabled = false;
            map.UiSettings.ZoomControlsEnabled = true;
            map.UiSettings.ZoomGesturesEnabled = true;

             
            startPin = new Pin
            {
                Type = PinType.SavedPin,
                Label = "Start Point",
            };
            endPin = new Pin
            {
                Type = PinType.Generic,
                Label = "End Point",
            };
            // Map Clicked

            map.MapClicked += async (sender, e) =>
            {
                if (mapMode == 2)
                {
                    destinationPos = e.Point;
                    endPin.Position = e.Point;

                    GeoResult result = await rest.GetGeoResult(e.Point.Latitude.ToString() + ',' + e.Point.Longitude.ToString());
                    if (result != null)
                    {
                        destination = result.results[0].formatted_address;
                        endPin.Address = result.results[0].formatted_address;
                    }
                    if (map.Pins.Contains(endPin))
                        map.Pins.Remove(endPin);
                    map.Pins.Add(endPin);
                    isDestinationSet = 2;
                }
                else if (mapMode == 1)
                {
                    originPos = e.Point;
                    startPin.Position = e.Point;

                    GeoResult result = await rest.GetGeoResult(e.Point.Latitude.ToString() + ',' + e.Point.Longitude.ToString());
                    if (result != null)
                    {
                        origin = result.results[0].formatted_address;
                        startPin.Address = result.results[0].formatted_address;
                    }
                    if (map.Pins.Contains(startPin))
                        map.Pins.Remove(startPin);
                    map.Pins.Add(startPin);
                    isHomeSet = 2;
                }

                mapMode = 0;

            };

        }

		async protected override void OnAppearing()
		{
            base.OnAppearing();
            try
            {
                if (isFirstLaunch)
                {
                    isFirstLaunch = false;
                    var status = await CrossPermissions.Current.CheckPermissionStatusAsync(Permission.Location);
                    if (status != PermissionStatus.Granted)
                    {
                        if (await CrossPermissions.Current.ShouldShowRequestPermissionRationaleAsync(Permission.Location))
                        {
                            await DisplayAlert("Need location", "Gunna need that location", "OK");
                        }

                        var results = await CrossPermissions.Current.RequestPermissionsAsync(Permission.Location);
                        //Best practice to always check that the key exists
                        if (results.ContainsKey(Permission.Location))
                            status = results[Permission.Location];
                    }

                    if (status == PermissionStatus.Granted)
                    {
                        if (Utils.IsLocationAvailable())
                        {
                            originPos = await Utils.GetPosition();
                            currentPos = originPos.Latitude + "," + originPos.Longitude;
                            map.MoveToRegion(MapSpan.FromCenterAndRadius(originPos, Distance.FromMeters(5000)));
                            GeoResult result = await rest.GetGeoResult(currentPos);
                            if (result != null)
                            {
                                origin = result.results[0].formatted_address;
                            }
                            else
                            {
                                origin = currentPos;
                            }
                            isHomeSet = 2;
                        }
                    }
                    else if (status != PermissionStatus.Unknown)
                    {
                        await DisplayAlert("Location Denied", "Can not continue, try again.", "OK");
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            //Setting up the locations
            if(isHomeSet > 0)
            {
                if(isHomeSet == 1)
                {
                    var result = await rest.GetGeoResult(origin);
                    if (result != null)
                        originPos = new Position(result.results[0].geometry.location.lat, result.results[0].geometry.location.lng);
                }

                startPin.Address = origin;
                if (map.Pins.Contains(startPin))
                    map.Pins.Remove(startPin);
                
                startPin.Position = originPos;
                map.Pins.Add(startPin);
            }

            if (isDestinationSet > 0)
            {
                if (isDestinationSet == 1)
                {
                    var result = await rest.GetGeoResult(destination);
                    if (result != null)
                        destinationPos = new Position(result.results[0].geometry.location.lat, result.results[0].geometry.location.lng);
                }
                endPin.Address = destination;
                if (map.Pins.Contains(endPin))
                    map.Pins.Remove(endPin);
                endPin.Position = destinationPos;
                map.Pins.Add(endPin);
            }
          
		}

        private List<Position> DecodePolyline(string encodedPoints)
        {
            if (string.IsNullOrWhiteSpace(encodedPoints))
                return null;

            int index = 0;
            var polylineChars = encodedPoints.ToCharArray();
            var poly = new List<Position>();
            int currentLat = 0;
            int currentLng = 0;
            int next5Bits;

            while (index < polylineChars.Length)
            {
                int sum = 0;
                int shifter = 0;

                do
                {
                    next5Bits = polylineChars[index++] - 63;
                    sum |= (next5Bits & 31) << shifter;
                    shifter += 5;
                }
                while (next5Bits >= 32 && index < polylineChars.Length);

                if (index >= polylineChars.Length)
                    break;

                currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);  
                sum = 0;
                shifter = 0;

                do
                {
                    next5Bits = polylineChars[index++] - 63;
                    sum |= (next5Bits & 31) << shifter;
                    shifter += 5;
                }
                while (next5Bits >= 32 && index < polylineChars.Length);

                if (index >= polylineChars.Length && next5Bits >= 32)
                {
                    break;
                }

                currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);
                var mLatLng = new Position(Convert.ToDouble(currentLat) / 100000.0, Convert.ToDouble(currentLng) / 100000.0);
                poly.Add(mLatLng);
            }

            return poly;
        }

        async void Go_Clicked(object sender, System.EventArgs e)
        {
            await CalculateRoute();
        }

        void Home_Focused(object sender, EventArgs e)
        {
            Navigation.PushModalAsync(new LocationSelectionPage(this, true));
        }

        void Destination_Focused(object sender, EventArgs e)
        {
            Navigation.PushModalAsync(new LocationSelectionPage(this, false));
        }

        async void Walking_Clicked(object sender, System.EventArgs e)
        {
            clearStyles();
            mode = 1;

            await CalculateRoute();
        }

        async void Bicycling_Clicked(object sender, System.EventArgs e)
        {
            clearStyles();
            mode = 0;

            await CalculateRoute();
        }

        void clearStyles()
        {
            
      
        }

        async Task<bool> CalculateRoute() 
        {
            map.Polylines.Clear();
            var line1 = new Xamarin.Forms.GoogleMaps.Polyline();
            line1.StrokeColor = Color.Blue;
            line1.StrokeWidth = 7;
            var line2 = new Xamarin.Forms.GoogleMaps.Polyline();
            line2.StrokeColor = Color.Magenta;
            line2.StrokeWidth = 4;

            string originParam = originPos.Latitude.ToString() + ',' + originPos.Longitude.ToString();
            string destinationParam = destinationPos.Latitude.ToString() + ',' + destinationPos.Longitude.ToString();
            if(isHomeSet == 1)
                originParam = origin;
            
            if (isDestinationSet == 1)
                destinationParam = destination;
            
            Bounds bounds = new Bounds(originPos, destinationPos);
            map.MoveToRegion(MapSpan.FromBounds(bounds));
            if (!string.IsNullOrEmpty(originParam) && !string.IsNullOrEmpty(destinationParam) && isHomeSet>0 && isDestinationSet>0)
            {
                //var distanceResult = await rest.GetDistance(originParam, destinationParam, modes[mode]);
                //if (distanceResult != null)
                //{
                //    if (distanceResult.rows[0].elements[0].distance != null)
                //    {
                //        string distance = distanceResult.rows[0].elements[0].distance.text;
                //        string duration = distanceResult.rows[0].elements[0].duration.text;
                //        distanceLabel.Text = $"Red Distance={distance}, Duration={duration}";
                //    }
                //}

                //var result = await rest.GetDirection(originParam, destinationParam, modes[mode]);

                //if (result != null)
                //{
                //    var line = new Xamarin.Forms.GoogleMaps.Polyline();
                //    line.StrokeColor = Color.Red;
                //    line.StrokeWidth = 10;
                //    foreach (var route in result.routes)
                //    {
                //        foreach (var leg in route.legs)
                //        {
                //            foreach (var step in leg.steps)
                //            {
                //                var points = DecodePolyline(step.polyline.points);
                //                foreach (var point in points)
                //                {
                //                    line.Positions.Add(point);
                //                }
                //            }
                //        }
                //    }
                //    if (line.Positions.Count >= 2)
                //        map.Polylines.Add(line);
                //}

                var mqResult = await rest.GetMQAlternativeDirection(originParam, destinationParam, mqModes[mode]);

                List<Position> pollutionPoints = new List<Position>();
               
                if (mqResult != null)
                {
                    if (mqResult.route != null)
                    {
                        if (mqResult.route.shape != null)
                        {
                            if (!string.IsNullOrEmpty(mqResult.route.shape.shapePoints))
                            {
                                var points = DecodePolyline(mqResult.route.shape.shapePoints);
                                foreach (var point in points)
                                {
                                    line1.Positions.Add(point);
                                    pollutionPoints.Add(point);
                                }
                                
                                if (line1.Positions.Count >= 2)
                                {
                                    map.Polylines.Add(line1);
                                }
                                maxPollution = await CalculatePollution(pollutionPoints, true);
                                drawHotspot();
                            }
                        }

                    }

                    if (mqResult.route.alternateRoutes != null)
                    {
                        bool duplicated = false;
                        foreach (var item in mqResult.route.alternateRoutes)
                        {
                            line2.Positions.Clear();
                            pollutionPoints.Clear();
                            if (item.route.shape != null)
                            {

                                if (!string.IsNullOrEmpty(item.route.shape.shapePoints))
                                {
                                    var points = DecodePolyline(item.route.shape.shapePoints);
                                    foreach (var point in points)
                                    {
                                        line2.Positions.Add(point);
                                        pollutionPoints.Add(point);
                                        if (point.Latitude.Equals(hotspot.Latitude) && point.Longitude.Equals(hotspot.Longitude))
                                        {
                                            duplicated = true;
                                            break;
                                        }
                                    }
                                    if (!duplicated)
                                    {
                                        float pollutionValue = await CalculatePollution(pollutionPoints, true);
                                        if (pollutionValue > maxPollution)
                                            continue;
                                        else
                                        {
                                            if (line2.Positions.Count >= 2)
                                            {
                                                map.Polylines.Add(line2);
                                            }
                                            drawHotspot();
                                            return true;
                                        }

                                    }
                                }
                            }

                        }
                    }


                }

            }
            else
            {
                await this.DisplayAlert("Warning", "Please fill all the fields", "OK");
            }
            return false;
        }

    
        async Task<float> CalculatePollution(List<Position> pollutionPoints, bool main=false) 
        {

            float overall = 0, max = 0;
            if(pollutionPoints.Count > 0) 
            {
                List<List<string>> list = new List<List<string>>();
                for (int i = 0; i < pollutionPoints.Count; i++)
                {
                    List<string> point = new List<string>();
                    point.Add(pollutionPoints[i].Latitude.ToString());
                    point.Add(pollutionPoints[i].Longitude.ToString());
                    list.Add(point);
                }
                PollutionRequest request = new PollutionRequest()
                {
                    RAD = 50,
                    PAIRS = list
                };
                var result = await rest.GetPollution(JsonConvert.SerializeObject(request));
                if(result!=null)
                {
                    int maxIndex = 0;
                    if (result.val.Count >= 5)
                    {
                        for (int index = 2; index < result.val.Count - 2; index++)
                        {
                            float subTotal = 0;
                            for (int j = index - 2; j < index + 2; j++)
                            {
                                subTotal += (float)Convert.ToDouble(result.val[j]);
                            }
                            if (subTotal > max)
                            {
                                max = subTotal;
                                maxIndex = index;
                                peak = max;
                            }
                  
                            overall += (float)Convert.ToDouble(result.val[index]);
                        }
                        overall /= result.val.Count;
                        double lat, lng;
                        lat = Convert.ToDouble(result.lat[maxIndex]);
                        lng = Convert.ToDouble(result.lon[maxIndex]);
                        hotspot = new Position(lat, lng);
                    }

                }
            }
            return overall;
        }
        void drawHotspot()
        {
            map.Pins.Remove(hotspotPin);
            hotspotPin = new Pin
            {
                Type = PinType.SavedPin,
                Label = "Hotspot: " + peak.ToString(),
                Position = hotspot,
            };
            map.Pins.Add(hotspotPin);
        }
    }
}
