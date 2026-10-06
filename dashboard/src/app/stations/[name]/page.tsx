import OverviewView from "@/components/views/OverviewView";

export default async function StationPage({ params }: { params: Promise<{ name: string }> }) {
  const { name } = await params;
  return <OverviewView station={decodeURIComponent(name)} />;
}
