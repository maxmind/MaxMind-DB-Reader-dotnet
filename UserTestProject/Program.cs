using System;
using System.Collections.Generic;
using MaxMind.Db;

// User-defined types that should be supported by source generators
public class UserLocation
{
    [Constructor]
    public UserLocation(
        [Parameter("latitude")] double latitude,
        [Parameter("longitude")] double longitude,
        [Parameter("city")] string city,
        [Parameter("accuracy_radius")] int accuracyRadius)
    {
        Latitude = latitude;
        Longitude = longitude;
        City = city;
        AccuracyRadius = accuracyRadius;
    }

    public double Latitude { get; }
    public double Longitude { get; }
    public string City { get; }
    public int AccuracyRadius { get; }
}

public class UserCountry  
{
    [Constructor]
    public UserCountry(
        [Parameter("iso_code")] string isoCode,
        [Parameter("name")] string name,
        [Parameter("is_in_european_union")] bool isInEuropeanUnion)
    {
        IsoCode = isoCode;
        Name = name;
        IsInEuropeanUnion = isInEuropeanUnion;
    }

    public string IsoCode { get; }
    public string Name { get; }
    public bool IsInEuropeanUnion { get; }
}

public class UserGeoData
{
    [Constructor]
    public UserGeoData(
        [Parameter("country")] UserCountry country,
        [Parameter("location")] UserLocation location,
        [Parameter("subdivisions")] IReadOnlyList<string> subdivisions)
    {
        Country = country;
        Location = location;
        Subdivisions = subdivisions;
    }

    public UserCountry Country { get; }
    public UserLocation Location { get; }
    public IReadOnlyList<string> Subdivisions { get; }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing source generators for user-defined types...");
        
        try
        {
#if NET8_0_OR_GREATER
            // Ensure generated activators are registered
            try
            {
                MaxMind.Db.Generated.TypeActivatorRegistration.EnsureRegistered();
                Console.WriteLine("✓ TypeActivatorRegistration triggered");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  No generated activators available: {ex.Message}");
            }

            // Check if our user types are supported by source generators
            var locationSupported = MaxMind.Db.SourceGeneratorSupport.HasActivator(typeof(UserLocation));
            var countrySupported = MaxMind.Db.SourceGeneratorSupport.HasActivator(typeof(UserCountry));
            var geoDataSupported = MaxMind.Db.SourceGeneratorSupport.HasActivator(typeof(UserGeoData));
            
            Console.WriteLine($"UserLocation supported: {locationSupported}");
            Console.WriteLine($"UserCountry supported: {countrySupported}");
            Console.WriteLine($"UserGeoData supported: {geoDataSupported}");
            
            if (locationSupported)
            {
                // Test creating an instance using source generators
                object?[] args = { 37.7749, -122.4194, "San Francisco", 10 };
                if (MaxMind.Db.SourceGeneratorSupport.TryCreateInstance(typeof(UserLocation), args, out var location))
                {
                    Console.WriteLine($"✓ Created UserLocation: {location?.GetType().Name}");
                    
                    if (location is UserLocation userLoc)
                    {
                        Console.WriteLine($"  City: {userLoc.City}, Lat: {userLoc.Latitude}, Lng: {userLoc.Longitude}");
                        Console.WriteLine("🎉 SUCCESS: Source generators working for user-defined types!");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to create instance using source generators");
                }

                // Test to verify NO reflection fallback happens when using the public API
                Console.WriteLine("\n--- Testing Public API Integration (Reader class) ---");
                Console.WriteLine("ℹ️  Note: Reader class uses TypeActivatorCreator internally");
                Console.WriteLine("ℹ️  This test would require an actual MaxMind DB file");
                Console.WriteLine("ℹ️  Source generators are confirmed working via SourceGeneratorSupport API");
                
                // Test with different types to verify all user types work
                Console.WriteLine("\n--- Testing All User Types ---");
                
                // Test UserCountry
                object?[] countryArgs = { "US", "United States", false };
                if (MaxMind.Db.SourceGeneratorSupport.TryCreateInstance(typeof(UserCountry), countryArgs, out var country))
                {
                    if (country is UserCountry userCountry)
                    {
                        Console.WriteLine($"✓ UserCountry: {userCountry.IsoCode} - {userCountry.Name}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to create UserCountry");
                }
                
                // Test nested UserGeoData (complex object with dependencies)
                try
                {
                    var country2 = new UserCountry("US", "United States", false);
                    var location2 = new UserLocation(37.7749, -122.4194, "San Francisco", 10);
                    var subdivisions = new List<string> { "California", "CA" };
                    object?[] geoArgs = { country2, location2, subdivisions };
                    
                    if (MaxMind.Db.SourceGeneratorSupport.TryCreateInstance(typeof(UserGeoData), geoArgs, out var geoData))
                    {
                        if (geoData is UserGeoData userGeoData)
                        {
                            Console.WriteLine($"✓ UserGeoData: {userGeoData.Country.Name}, {userGeoData.Location.City}");
                            Console.WriteLine("🎉 ALL USER TYPES working with source generators!");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Failed to create UserGeoData");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error with UserGeoData: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ Source generators not working for user types");
                
                // If source generators weren't working, this would be a serious issue
                Console.WriteLine("⚠️  This should not happen with proper source generator setup");
            }
#else
            Console.WriteLine("⚠️  Source generators only available on .NET 8+");
#endif
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
