module TempleEyeExt

using ..Ahorn, Maple

const placements = Ahorn.PlacementDict(
    "Temple Eye (Small, Follow Madeline)" => Ahorn.EntityPlacement(
        Maple.TempleEye,
        "point",
        Dict{String, Any}(
            "followMadeline" => true
        )
    )
)

end