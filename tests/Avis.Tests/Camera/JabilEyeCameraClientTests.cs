using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avis.Camera;
using Avis.Configuration;
using Avis.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Avis.Tests.Camera;

public class JabilEyeCameraClientTests
{
    private static JabilEyeCameraOptions MakeOptions() => new()
    {
        HostName = "192.168.1.50",
        ProgramName = "MyProgram",
        ResourceName = "Burn In",
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };

    private static JabilEyeCameraClient MakeClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), MakeOptions(), NullLogger<JabilEyeCameraClient>.Instance);

    // Confirmed via live testing against a real camera: its embedded HTTP server
    // doesn't reliably support connection reuse - a connection pooled from a prior
    // request produced "response ended prematurely" errors on the next one. Every
    // request must ask for a fresh connection instead of relying on HttpClient's
    // default pooling.
    [Fact]
    public async Task PostRequests_SetConnectionClose()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.True(req.Headers.ConnectionClose);
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).OpenProgramAsync("MyProgram");
    }

    [Fact]
    public async Task OpenProgramAsync_PostsToCorrectUrl()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("http://192.168.1.50/api/public/v1.0/programs/MyProgram/open", req.RequestUri!.ToString());
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).OpenProgramAsync("MyProgram");
    }

    [Fact]
    public async Task StartLiveTrigger_SendsState2_StopSendsState1()
    {
        var bodies = new List<string>();
        var handler = new FakeHttpMessageHandler(async req =>
        {
            Assert.Equal("http://192.168.1.50/api/public/v1.0/programs/0/execute", req.RequestUri!.ToString());
            bodies.Add(await req.Content!.ReadAsStringAsync());
            return JsonResponse(HttpStatusCode.OK, new { });
        });
        var client = MakeClient(handler);

        await client.StartLiveTriggerAsync();
        await client.StopLiveTriggerAsync();

        Assert.Equal("{\"state\":2}", bodies[0]);
        Assert.Equal("{\"state\":1}", bodies[1]);
    }

    [Fact]
    public async Task TriggerProgramAsync_NoArgs_SendsEmptyBody()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).TriggerProgramAsync();

        Assert.Equal("{}", body);
    }

    [Fact]
    public async Task TriggerProgramAsync_WithSerialAndOperator_SendsDataCollection()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).TriggerProgramAsync(assemblySerialNumber: "DEVSRV000010", operatorId: "4375789");

        using var doc = JsonDocument.Parse(body!);
        var collection = doc.RootElement.GetProperty("data_collection");
        Assert.Equal(2, collection.GetArrayLength());
        Assert.Equal("JE_ASSEMBLY_SERIAL_NUMBER", collection[0].GetProperty("key").GetString());
        Assert.Equal("DEVSRV000010", collection[0].GetProperty("value").GetString());
        Assert.Equal("JE_OPERATOR_ID", collection[1].GetProperty("key").GetString());
        Assert.Equal("4375789", collection[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task StartJobAsync_IncludesState2AlongsideDataCollection()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).StartJobAsync(assemblySerialNumber: "DEVSRV000010");

        using var doc = JsonDocument.Parse(body!);
        Assert.Equal(2, doc.RootElement.GetProperty("state").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("data_collection").GetArrayLength());
    }

    [Fact]
    public async Task TriggerJobByIndexAsync_SendsJobIndex()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).TriggerJobByIndexAsync(0);

        Assert.Equal("{\"job_index\":0}", body);
    }

    [Fact]
    public async Task TriggerJobByIdAsync_SendsJobId()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).TriggerJobByIdAsync("JOB ID HERE");

        Assert.Equal("{\"job_id\":\"JOB ID HERE\"}", body);
    }

    [Fact]
    public async Task EndJobAsync_SendsState1()
    {
        string? body = null;
        var handler = new FakeHttpMessageHandler(async req =>
        {
            body = await req.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, new { });
        });

        await MakeClient(handler).EndJobAsync();

        Assert.Equal("{\"state\":1}", body);
    }

    // Verbatim shape from the JabilEye API/MQTT Return Definition example
    // (Software Reference Guide, Chapter 8) - a program with one job and one task.
    private const string FullResultExampleJson = """
        {
          "program_id": "07834a40-10f3-11ef-9fed-0242ac120003",
          "program_description": "",
          "program_execution": {
            "execution_id": "9fbedbea-10f3-11ef-9fed-0242ac120003",
            "execution_start_time": "2024-05-14 02:27:37",
            "execution_end_time": "2024-05-14 02:27:37",
            "execution_result": 2,
            "execution_time": 301
          },
          "program_jobs": [
            {
              "job_id": "9a6a9900-10f3-11ef-9fed-0242ac120003",
              "job_description": "",
              "job_name": "Test1",
              "job_execution": {
                "execution_id": "9fbedbeb-10f3-11ef-9fed-0242ac120003",
                "execution_start_time": "2024-05-14 02:27:37",
                "execution_end_time": "2024-05-14 02:27:37",
                "execution_result": 2,
                "execution_time": 179
              },
              "job_tasks": [
                {
                  "task_description": "",
                  "task_enabled": true,
                  "task_id": "9e68282c-10f3-11ef-9fed-0242ac120003",
                  "task_name": "Task1",
                  "task_tool": 1003,
                  "task_execution": {
                    "execution_id": "9fbedbec-10f3-11ef-9fed-0242ac120003",
                    "execution_start_time": "2024-05-14 02:27:37",
                    "execution_end_time": "2024-05-14 02:27:37",
                    "execution_result": 2,
                    "execution_time": 152,
                    "execution_image": "/9j/4AAQSkZJRgABAQEA..."
                  },
                  "task_region": {
                    "region_centroid": { "x_coordinate": 1885, "y_coordinate": 875 },
                    "region_height": 552,
                    "region_rotation": 0.0,
                    "region_width": 552
                  },
                  "task_output": "DEVSRV000010"
                }
              ]
            }
          ],
          "program_filename": "new-program"
        }
        """;

    [Fact]
    public async Task GetResultsAsync_SetsConnectionClose()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.True(req.Headers.ConnectionClose);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FullResultExampleJson, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        await MakeClient(handler).GetResultsAsync();
    }

    [Fact]
    public async Task GetResultsAsync_ParsesFullExampleFromApiDocs()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("http://192.168.1.50/api/public/v1.0/programs/0/results", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FullResultExampleJson, System.Text.Encoding.UTF8, "application/json"),
            };
        });

        var result = await MakeClient(handler).GetResultsAsync();

        Assert.Equal("07834a40-10f3-11ef-9fed-0242ac120003", result.ProgramId);
        Assert.Equal("9fbedbea-10f3-11ef-9fed-0242ac120003", result.ExecutionId);
        Assert.Equal(JabilEyeResultCode.Pass, result.ExecutionResult);
        Assert.Single(result.Jobs);
        var job = result.Jobs[0];
        Assert.Equal("Test1", job.JobName);
        Assert.Equal(JabilEyeResultCode.Pass, job.ExecutionResult);
        Assert.Single(job.Tasks);
        var task = job.Tasks[0];
        Assert.Equal(1003, task.TaskTool); // CHARACTER_READING_V1
        Assert.Equal(JabilEyeResultCode.Pass, task.ExecutionResult);
        Assert.Equal("DEVSRV000010", task.TaskOutput!.Value.GetString());
    }

    // Verbatim shape from the docs for a program that hasn't been executed yet.
    private const string NotExecutedExampleJson = """
        {
          "program_id": "07834a40-10f3-11ef-9fed-0242ac120003",
          "program_description": "",
          "program_execution": {
            "execution_end_time": "1970-01-01 00:00:00",
            "execution_id": "",
            "execution_result": 0,
            "execution_start_time": "1970-01-01 00:00:00",
            "execution_time": 0
          },
          "program_jobs": [],
          "program_filename": "nVidia"
        }
        """;

    [Fact]
    public async Task GetResultsAsync_NotYetExecuted_ExecutionResultIsNull()
    {
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(NotExecutedExampleJson, System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await MakeClient(handler).GetResultsAsync();

        Assert.Null(result.ExecutionResult);
        Assert.Empty(result.ExecutionId);
        Assert.Empty(result.Jobs);
        Assert.Equal("nVidia", result.ProgramFilename);
    }

    // Confirmed via live testing: unlike the docs' example, this camera sends
    // task_tool and execution_result as JSON strings, not JSON numbers.
    private const string StringEncodedNumbersExampleJson = """
        {
          "program_id": "07834a40-10f3-11ef-9fed-0242ac120003",
          "program_description": "",
          "program_execution": {
            "execution_id": "9fbedbea-10f3-11ef-9fed-0242ac120003",
            "execution_start_time": "2024-05-14 02:27:37",
            "execution_end_time": "2024-05-14 02:27:37",
            "execution_result": "2",
            "execution_time": 301
          },
          "program_jobs": [
            {
              "job_id": "9a6a9900-10f3-11ef-9fed-0242ac120003",
              "job_description": "",
              "job_name": "Test1",
              "job_execution": {
                "execution_id": "9fbedbeb-10f3-11ef-9fed-0242ac120003",
                "execution_start_time": "2024-05-14 02:27:37",
                "execution_end_time": "2024-05-14 02:27:37",
                "execution_result": "2",
                "execution_time": 179
              },
              "job_tasks": [
                {
                  "task_description": "",
                  "task_enabled": true,
                  "task_id": "9e68282c-10f3-11ef-9fed-0242ac120003",
                  "task_name": "Task1",
                  "task_tool": "1005",
                  "task_execution": {
                    "execution_id": "9fbedbec-10f3-11ef-9fed-0242ac120003",
                    "execution_start_time": "2024-05-14 02:27:37",
                    "execution_end_time": "2024-05-14 02:27:37",
                    "execution_result": "2",
                    "execution_time": 152
                  },
                  "task_output": "DEVSRV000010"
                }
              ]
            }
          ],
          "program_filename": "new-program"
        }
        """;

    [Fact]
    public async Task GetResultsAsync_ToleratesStringEncodedNumbers()
    {
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StringEncodedNumbersExampleJson, System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await MakeClient(handler).GetResultsAsync();

        Assert.Equal(JabilEyeResultCode.Pass, result.ExecutionResult);
        var job = Assert.Single(result.Jobs);
        Assert.Equal(JabilEyeResultCode.Pass, job.ExecutionResult);
        var task = Assert.Single(job.Tasks);
        Assert.Equal(1005, task.TaskTool);
        Assert.Equal(JabilEyeResultCode.Pass, task.ExecutionResult);
    }

    // Captured verbatim (trimmed of the base64 execution_image) from a real capture
    // on the actual Station-01 camera: task_tool is "<code>-<NAME>", not a bare
    // number or a plain numeric string like the docs and our first fix assumed.
    private const string RealCameraCaptureJson = """
        {
          "program_id": "07834a40-10f3-11ef-9fed-0242ac120003",
          "program_execution": {
            "execution_id": "20260902-174415565735",
            "execution_result": 2,
            "execution_start_time": "2026-09-02 17:44:15.785",
            "execution_end_time": "2026-09-02 17:44:15.935",
            "execution_time": 213
          },
          "program_jobs": [
            {
              "job_id": "job1",
              "job_name": "Job1",
              "job_execution": {
                "execution_id": "job-exec-1",
                "execution_result": 2
              },
              "job_tasks": [
                {
                  "task_id": "4113bedc-a711-11f1-bb7e-0242ac120003",
                  "task_name": "Task1",
                  "task_tool": "1001-OBJECT_LOCATOR_V1",
                  "task_execution": { "execution_id": "task-exec-1", "execution_result": 2 },
                  "task_output": { "angle": -0.061359234154221766, "scale": 1.006250023841858, "score": 0.9641631245613098 }
                },
                {
                  "task_id": "7031d3a2-a711-11f1-9637-0242ac120003",
                  "task_name": "Task2",
                  "task_tool": "1005-CODE_READING_V1",
                  "task_execution": { "execution_id": "task-exec-2", "execution_result": 2 },
                  "task_output": {
                    "results": [
                      { "format": "CODE128", "formatted_text": "", "text": "3541042699", "quality": 7 }
                    ]
                  }
                }
              ]
            }
          ],
          "program_filename": "AssetID1"
        }
        """;

    [Fact]
    public async Task GetResultsAsync_RealCameraCapture_ParsesCodeReadingTaskTool()
    {
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(RealCameraCaptureJson, System.Text.Encoding.UTF8, "application/json"),
        });

        var result = await MakeClient(handler).GetResultsAsync();

        Assert.Equal(JabilEyeResultCode.Pass, result.ExecutionResult);
        var tasks = result.Jobs.SelectMany(j => j.Tasks).ToList();
        Assert.Equal(1001, tasks[0].TaskTool);
        Assert.Equal(1005, tasks[1].TaskTool);
        Assert.Equal("3541042699", AssetIdExtractor.Extract(result, assetIdToolCode: 1005));
    }
}
