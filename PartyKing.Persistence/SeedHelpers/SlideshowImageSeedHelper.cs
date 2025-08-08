using PartyKing.Domain.Entities;

namespace PartyKing.Persistence.SeedHelpers;

public static class SlideshowImageSeedHelper
{
    public static IReadOnlyCollection<SlideshowImage> GetData()
    {
        return
        [
            new SlideshowImage(
                new Guid("01988505-D093-7975-B654-E08CCF29268F"),
                "image/png",
                "SampleImages/Uploaded/IMG_2332.png",
                "IMG_2332.png"),
            new SlideshowImage(
                new Guid("01988506-1BB9-7308-A454-183052930AD6"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_7758.jpeg",
                "IMG_7758.jpeg"),
            new SlideshowImage(
                new Guid("01988506-4EEE-760B-AEA7-9CBAB5F126F6"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_7296.jpeg",
                "IMG_7296.jpeg"),
            new SlideshowImage(
                new Guid("01988506-9009-7274-9825-A38A703EC929"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_6050.jpeg",
                "IMG_6050.jpeg"),
            new SlideshowImage(
                new Guid("01988506-BFAF-7985-B161-D1555555A762"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_5420.jpeg",
                "IMG_5420.jpeg"),
            new SlideshowImage(
                new Guid("01988507-0487-7618-BC80-BBECFEC7479C"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_5565.jpeg",
                "IMG_5565.jpeg"),
            new SlideshowImage(
                new Guid("01988507-2F0C-758B-B577-65CEEB9B1DBE"),
                "image/png",
                "SampleImages/Uploaded/IMG_4948.png",
                "IMG_4948.png"),
            new SlideshowImage(
                new Guid("01988507-52AE-71DF-9A53-918A85A61A25"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_4943.jpeg",
                "IMG_4943.jpeg"),
            new SlideshowImage(
                new Guid("01988507-AC6B-73AE-A586-902F2A46F4B0"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_4965.jpeg",
                "IMG_4965.jpeg"),
            new SlideshowImage(
                new Guid("01988507-F7BC-73CC-8505-12313D4419E3"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_4437.jpeg",
                "IMG_4437.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-73DD-AE67-29B2EDF9F268"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3375.jpeg",
                "IMG_3375.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-708D-AA86-4C8EDB216623"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3380.jpeg",
                "IMG_3380.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-76F2-A1D2-BFD6C4C213DF"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3379.jpeg",
                "IMG_3379.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-7855-9C0C-52C5524614B4"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3382.jpeg",
                "IMG_3382.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-744E-9811-ECA74C350A98"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3377.jpeg",
                "IMG_3377.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-7BE5-A608-95918F46D42C"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3381.jpeg",
                "IMG_3381.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-7BFD-94A8-8D8AAD6CDEB2"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3378.jpeg",
                "IMG_3378.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-79DA-8602-C6C2C11DAE72"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3374.jpeg",
                "IMG_3374.jpeg"),
            new SlideshowImage(
                new Guid("01988512-F059-718B-AFFA-B04BDD32F330"),
                "image/jpeg",
                "SampleImages/Uploaded/IMG_3376.jpeg",
                "IMG_3376.jpeg")
        ];
    }
}