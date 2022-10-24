case $1 in
  --clean)
    dotnet clean
    dotnet run
    break
    ;;
  --build)
    dotnet build
    dotnet run
    break
    ;;
  *)
  dotnet run
esac