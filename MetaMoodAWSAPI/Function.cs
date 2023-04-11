using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MetaMoodAWSAPI.DTOs;
using MetaMoodAWSAPI.Entities;
using MetaMoodAWSAPI.QueryParameterModels;
using MetaMoodAWSAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using System.Data;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MetaMoodAWSAPI;

public class Function
{
    private readonly IServiceCollection _serviceCollection;
    private readonly MetaMoodContext _DBContext;

    /// <summary>
    /// This is the constructor for the Lambda function that sets up the collection of services, calls RegisterServices(),
    /// and provides a database context for this class. 
    /// </summary>
    public Function()
    {
        _serviceCollection = new ServiceCollection();
        ServiceProvider serviceProvider = _serviceCollection.RegisterServices().BuildServiceProvider();
        _DBContext = serviceProvider.GetRequiredService<MetaMoodContext>();
    }

    /// <summary>
    /// Constructor that allows injection of DBContext. This method is to allow unit tests to inject their own DB context.
    /// </summary>
    /// <param name="dbContext">The database context created in the test project</param>
    public Function(MetaMoodContext dbContext)
    {
        _serviceCollection = new ServiceCollection();
        _serviceCollection.RegisterServices().BuildServiceProvider();
        _DBContext = dbContext;
    }



    /// <summary>
    /// This function makes an asynchronous request to the database to retrieve and return a page of tracks that
    /// fits the given criteria.
    /// </summary>
    /// <param name="request">request contains a dictionary that has all the query parameters necessary to
    /// determine any search and sort criteria and paging parameters.</param>
    /// <param name="context"></param>
    /// <returns>A selected page of tracks from the spotify tracks table</returns>
    public async Task<APIGatewayHttpApiV2ProxyResponse> GetTrackPageAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        SpotifyParameters spotifyParameters = new ();

        try
        {
            spotifyParameters = QueryParameterService.GetSpotifyQueryParameters(spotifyParameters, request.QueryStringParameters);
        }
        catch (Exception ex)
        {
            return Response.BadRequest(ex.Message);
        }

        IList<SpotifyTrackDTO> tracks = await _DBContext.SpotifyTracks.Select(
        t => new SpotifyTrackDTO
        {
            Name = t.Name,
            ReleaseDate = t.ReleaseDate,
            Popularity = t.Popularity,
            Acousticness = t.Acousticness,
            Danceability = t.Danceability,
            Energy = t.Energy,
            Liveness = t.Liveness,
            Loudness = t.Loudness,
            Speechiness = t.Speechiness,
            Tempo = t.Tempo,
            Instrumentalness = t.Instrumentalness,
            Valence = t.Valence
        }
        )
        .SpotifyTrackSearchBy<SpotifyTrackDTO>(spotifyParameters)
        .GetPage(spotifyParameters.PageSize, spotifyParameters.PageNumber)
        .SpotifyTrackSortBy<SpotifyTrackDTO>(spotifyParameters.SortBy)
        .ToListAsync();
        

        if (tracks.Count <= 0)
        {
            return Response.NotFound();
        }
        else
        {
            return Response.OK(tracks);
        }

    }

    /// <summary>
    /// This function returns the number of records in a given table. 
    /// </summary>
    /// <remarks>This function will mostly be used to determine how many pages of data there should be.</remarks>
    /// <param name="request">request contains a path parameter that says which table to get the count of</param>
    /// <param name="context"></param>
    /// <returns>The number of records in a given table.</returns>
    public APIGatewayHttpApiV2ProxyResponse GetCount(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        int count = 0;

        string table;
        try
        {
            table = QueryParameterService.GetCountParameters(request.PathParameters);
        }
        catch (Exception ex)
        {
            return Response.BadRequest(ex.Message);
        }

        switch (table)
        {
            case "spotify-tracks":
                using (var sqlConnection1 = new MySqlConnection(System.Environment.GetEnvironmentVariable("ConnectionString")))
                {
                    using (var cmd = new MySqlCommand()
                    {
                        CommandText = $"SELECT `spotify-tracks` FROM counts",
                        CommandType = CommandType.Text,
                        Connection = sqlConnection1
                    })
                    {
                        sqlConnection1.Open();

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                count = (int)reader[0];
                            }
                        }
                    }
                }
                break;
            default:
                return Response.NotFound();
        }


        if (count < 0)
        {
            return Response.NotFound();
        }
        else
        {
            return Response.OKCount(count);
        }

    }

    /// <summary>
    /// This function returns a list of strings containing the data for the bar graph that has the average values for each category
    /// with a floating point value (besides loudness). 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns>A list of doubles converted to strings that will be used for the Chart.js bargraph with Spotify metric averages</returns>
    public APIGatewayHttpApiV2ProxyResponse GetSpotifyAverages(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        IList<string> data = new List<string>();

        using (var sqlConnection1 = new MySqlConnection(System.Environment.GetEnvironmentVariable("ConnectionString")))
        {
            using (var cmd = new MySqlCommand()
            {
                CommandText = $"SELECT * FROM spotify_metric_averages",
                CommandType = CommandType.Text,
                Connection = sqlConnection1
            })
            {
                sqlConnection1.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        data.Add(((double)reader[0]).ToString() ?? "0.00");
                        data.Add(((double)reader[1]).ToString() ?? "0.00");
                        data.Add(((double)reader[2]).ToString() ?? "0.00");
                        data.Add(((double)reader[3]).ToString() ?? "0.00");
                        data.Add(((double)reader[4]).ToString() ?? "0.00");
                        data.Add(((double)reader[5]).ToString() ?? "0.00");
                        data.Add(((double)reader[6]).ToString() ?? "0.00");
                    }
                }
            }
        }

        if (data.Count <= 0)
        {
            return Response.NotFound();
        }
        else
        {
            return Response.OK(data);
        }

    }

}
